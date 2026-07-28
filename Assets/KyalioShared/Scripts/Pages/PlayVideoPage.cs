using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.State;
using Kyalio.Utils;
using AppPlaybackState = Kyalio.State.PlaybackState;
using AppState = Kyalio.State.V2.AppState;
using RenderHeads.Media.AVProVideo;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Video player page powered by AVPro MediaPlayer.
    ///
    /// Param: ValueTuple&lt;string, PlaylistItem&gt; — (projectId, episode to play).
    /// Set by ProjectInfoPage (full playlist) or SeriesPage (single episode).
    ///
    /// On enter:
    ///   — Hides TabBar and all other pages (UIManager handles page hiding).
    ///   — Prefers a locally downloaded file; falls back to a stream URL from the API.
    ///   — Seeks to the episode's last saved progress (from the progress cache) before playing.
    ///   — Syncs watch progress every 10 s while playing.
    ///   — Refreshes the stream URL automatically 60 s before token expiry.
    ///
    /// On exit:
    ///   — Saves final watch progress.
    ///   — Closes media and restores TabBar.
    /// </summary>
    public class PlayVideoPage : MonoBehaviour, IPageHandler
    {
        [Header("AVPro")]
        [SerializeField] private MediaPlayer _mediaPlayer;
        [SerializeField] private GameObject _avProRoot;
        [SerializeField] private VideoSphereRecenter _sphereRecenter;

        [Header("Navigation")]
        [SerializeField] private GameObject _tabBarRoot;
        [SerializeField] private Button _homeButton;

        [Header("Playback Controls")]
        [SerializeField] private Button _playPauseButton;
        [SerializeField] private Image _playPauseIcon;
        [SerializeField] private Sprite _playSprite;
        [SerializeField] private Sprite _pauseSprite;
        [SerializeField] private Button _skipForwardButton;
        [SerializeField] private Button _skipBackwardButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _prevButton;

        [Header("Progress")]
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _currentTimeText;
        [SerializeField] private TextMeshProUGUI _durationText;

        [Header("Speed")]
        [SerializeField] private Button _speedButton;
        [SerializeField] private TextMeshProUGUI _speedText;
        [SerializeField] private GameObject _speedPanel;
        [SerializeField] private Button _speedPanelCloseButton;
        [SerializeField] private Toggle _speed1Toggle;
        [SerializeField] private Toggle _speed125Toggle;
        [SerializeField] private Toggle _speed15Toggle;

        [Header("Controls Auto-Hide")]
        [Tooltip("CanvasGroup on vr_video_controll_ui. Toggled via alpha/interactable so the " +
                 "page handler (on the same object) keeps running while the controls are hidden.")]
        [SerializeField] private CanvasGroup _controlsGroup;
        [Tooltip("Seconds of inactivity (while playing) before the controls auto-hide.")]
        [SerializeField] private float _controlsHideDelay = 5f;
        [Tooltip("Trigger actions that re-summon the controls — drag the Activate action " +
                 "from XRI Left Interaction and XRI Right Interaction (XRI Default Input Actions).")]
        [SerializeField] private InputActionReference[] _showControlsActions;

        [Header("Controls Show — Meta SDK")]
        [Tooltip("Controllers are polled via OVRInput (index trigger) — no wiring needed. " +
                 "For hand tracking, drag the left/right OVRHand so an index-finger pinch " +
                 "(thumb + index touch) re-summons the controls.")]
        [SerializeField] private OVRHand _leftHand;
        [SerializeField] private OVRHand _rightHand;

        // ── Runtime ───────────────────────────────────────────────────

        // The whole UI lives under an OVROverlayCanvas (Meta composition layer).
        // In PunchAHole/Underlay mode the controls are composited *behind* the eye
        // buffer, so the opaque video occludes them. While the video page is open we
        // disable the overlay; the canvas then falls back to its imposter mesh, which
        // renders the controls in the eye buffer *on top* of the video (the behaviour
        // we had before switching to the Meta UI). Restored on exit.
        private OVROverlayCanvas _overlayCanvas;

        private string _projectId;
        private PlaylistItem _currentItem;
        private CancellationTokenSource _cts;
        private readonly StreamExpiryChecker _expiryChecker = new();

        private float _playbackRate = 1f;
        private int _pendingResumeMs;
        private bool _preventSliderCallback;

        private float _watchSyncTimer;
        private string _knownProgressUpdatedAt;

        // Trigger press summons the controls; idle timer hides them again.
        private float _controlsIdleTimer;

        // Show-controls input is only polled while the page is active (set in
        // SubscribeShowControls / cleared in UnsubscribeShowControls).
        private bool _showControlsInputActive;
        private bool _leftPinching;
        private bool _rightPinching;

        // Controller trigger → summon controls. Built in code (no Inspector wiring)
        // and fed by OpenXR through the Input System — OVRInput's controller side
        // doesn't fire reliably in this OpenXR rig, but OVRHand (pinch) does.
        private InputAction _triggerShowAction;

        private const double SkipSeconds = 10.0;
        private const float WatchSyncIntervalSecs = 10f;

        // ── Unity lifecycle ───────────────────────────────────────────

        private void Awake()
        {
            _homeButton?.onClick.AddListener(OnHomeClicked);
            _playPauseButton?.onClick.AddListener(OnPlayPauseClicked);
            _skipForwardButton?.onClick.AddListener(OnSkipForward);
            _skipBackwardButton?.onClick.AddListener(OnSkipBackward);
            _nextButton?.onClick.AddListener(OnNextClicked);
            _prevButton?.onClick.AddListener(OnPrevClicked);

            _progressSlider?.onValueChanged.AddListener(OnSliderChanged);

            _speedButton?.onClick.AddListener(() =>
                _speedPanel?.SetActive(!(_speedPanel?.activeSelf ?? false)));
            _speedPanelCloseButton?.onClick.AddListener(() =>
                _speedPanel?.SetActive(false));

            _speed1Toggle?.onValueChanged.AddListener(on => { if (on) SetSpeed(1f); });
            _speed125Toggle?.onValueChanged.AddListener(on => { if (on) SetSpeed(1.25f); });
            _speed15Toggle?.onValueChanged.AddListener(on => { if (on) SetSpeed(1.5f); });

            if (_mediaPlayer != null)
                _mediaPlayer.Events.AddListener(OnMediaPlayerEvent);

            _overlayCanvas = GetComponentInParent<OVROverlayCanvas>(includeInactive: true);

            // Any XR controller's analog trigger (Button action fires once per press
            // past the default threshold). Matches both controllers via the generic
            // <XRController> path, so no per-hand binding is required.
            _triggerShowAction = new InputAction("ShowControlsTrigger", InputActionType.Button);
            _triggerShowAction.AddBinding("<XRController>/trigger");
            _triggerShowAction.performed += OnShowControlsInput;
        }

        private void OnDestroy()
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Events.RemoveListener(OnMediaPlayerEvent);

            if (_triggerShowAction != null)
            {
                _triggerShowAction.performed -= OnShowControlsInput;
                _triggerShowAction.Dispose();
            }
        }

        private void Update()
        {
            // Poll before the media guard so the trigger / pinch can summon the
            // controls even while paused or before the media has opened.
            PollShowControlsInput();

            if (_mediaPlayer?.Control == null || !_mediaPlayer.MediaOpened) return;

            double duration = _mediaPlayer.Info?.GetDuration() ?? 0;
            double current = _mediaPlayer.Control.GetCurrentTime();

            // Scrub bar — guard against re-entrant callbacks
            if (duration > 0 && !_preventSliderCallback)
            {
                _preventSliderCallback = true;
                _progressSlider?.SetValueWithoutNotify((float)(current / duration));
                _preventSliderCallback = false;
            }

            UpdateTimeText(current, duration);
            AppPlaybackState.Instance.CurrentPositionMs = (long)(current * 1000.0);
            RefreshPlayPauseButton();

            // Auto-hide the controls after inactivity, but only while actually
            // playing — keep them up while paused so the user can act.
            // Set _controlsHideDelay <= 0 to keep the controls permanently visible.
            if (_controlsHideDelay > 0f && ControlsVisible && _mediaPlayer.Control.IsPlaying())
            {
                _controlsIdleTimer += Time.deltaTime;
                if (_controlsIdleTimer >= _controlsHideDelay)
                    HideControls();
            }

            // Periodic watch-history sync while playing
            if (_mediaPlayer.Control.IsPlaying() && !DevFlags.UseFakeData)
            {
                _watchSyncTimer += Time.deltaTime;
                if (_watchSyncTimer >= WatchSyncIntervalSecs)
                {
                    _watchSyncTimer = 0f;
                    SyncWatchProgressAsync(CancellationToken.None).Forget();
                }
            }
        }

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            if (param is ValueTuple<string, PlaylistItem> t)
            {
                _projectId = t.Item1;
                _currentItem = t.Item2;
            }

            if (_avProRoot != null) _avProRoot.SetActive(true);

            // 球體啟用後立刻置中：球心對到眼睛高度，影片正面轉向使用者當下的朝向。
            // 必須在 SetActive(true) 之後，元件的 Awake 才跑得到。
            _sphereRecenter?.Recenter();

            if (_tabBarRoot != null) _tabBarRoot.SetActive(false);
            _speedPanel?.SetActive(false);

            // Render the controls in the eye buffer (imposter) so they draw on top of
            // the video instead of being occluded behind the underlay composition layer.
            if (_overlayCanvas != null) _overlayCanvas.overlayEnabled = false;

            // Controls start visible; the idle timer (counted only while playing)
            // hides them ~5 s after playback begins.
            ShowControls();
            SubscribeShowControls();

            // 進入播放頁時隱藏 3D 場景並把相機天空盒改純色。
            // 切頁的 fade 由 UIManager 負責（GoTo(..., fade: true)），這裡即時切換即可。
            CinemaModeController.Instance?.Enter(fade: false);

            _watchSyncTimer = 0f;
            _knownProgressUpdatedAt = ProgressUpdatedAt(_currentItem);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            SetSpeed(1f);
            RefreshNavButtons();
            UpdateTimeText(0, 0);
            if (_mediaPlayer != null) _mediaPlayer.Loop = false;

            LoadAndPlayAsync(_projectId, _currentItem, _cts.Token).Forget();
        }

        public void OnExit()
        {
            UnsubscribeShowControls();
            _expiryChecker.Stop();
            _cts?.Cancel();

            // Save final progress with a fresh token — page is closing but the request
            // must be allowed to complete.
            if (!DevFlags.UseFakeData && _currentItem != null)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            AppState.Instance.MarkWatchHistoryDirty();

            // 回到主頁面時還原 3D 場景與天空盒（切頁 fade 由 UIManager.GoBack(fade: true) 負責，
            // 故即時切換；影片若已自然結束則已還原，這裡會被守門略過）
            CinemaModeController.Instance?.Exit(fade: false);

            _mediaPlayer?.Control?.Pause();
            _mediaPlayer?.CloseMedia();

            if (_avProRoot != null) _avProRoot.SetActive(false);
            if (_tabBarRoot != null) _tabBarRoot.SetActive(true);

            // Restore the Meta composition-layer rendering for the menu UI.
            if (_overlayCanvas != null) _overlayCanvas.overlayEnabled = true;
        }

        // ── Load & play ───────────────────────────────────────────────

        private async UniTaskVoid LoadAndPlayAsync(
            string projectId, PlaylistItem item, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(item?.VideoId)) return;

            int resumeMs = ProjectCacheRepository.Instance.GetProgressMs(item.VideoId);

            // 1. Prefer locally downloaded file
            var localPath = DownloadedVideoState.Instance
                .GetFilePath(projectId, item.VideoId);

            if (localPath != null)
            {
                OpenMedia(localPath, resumeMs);
                return;
            }

            // 2. Dev mode — no API available
            if (DevFlags.UseFakeData)
            {
                Debug.LogWarning(
                    "[PlayVideoPage] No local download found; skipping stream fetch in dev mode.");
                return;
            }

            // 3. Fetch stream URL from API
            try
            {
                var stream = await ServiceLocator.Instance.V2.Stream
                    .GetStreamAsync(projectId, item.VideoId, ct);
                if (ct.IsCancellationRequested) return;

                AppPlaybackState.Instance.SetPlayback(projectId, item, stream);
                OpenMedia(stream.StreamUrl, resumeMs);

                // Schedule a URL refresh 60 s before the token expires
                _expiryChecker.Stop();
                _expiryChecker.Start(stream.ExpiresAt, async () =>
                {
                    await RefreshStreamUrlAsync(ct);
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"[PlayVideoPage] Stream fetch failed: {e.Message}");
            }
        }

        /// <summary>Opens media at the given path and stores the resume position.</summary>
        private void OpenMedia(string path, int resumeMs)
        {
            _pendingResumeMs = resumeMs;
            _mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, path, autoPlay: false);
        }

        private async UniTask RefreshStreamUrlAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested || _currentItem == null) return;

            try
            {
                // Remember current position so we can seek back after re-open
                double savedPos = _mediaPlayer.Control?.GetCurrentTime() ?? 0;

                var stream = await ServiceLocator.Instance.V2.Stream
                    .GetStreamAsync(_projectId, _currentItem.VideoId, ct);
                if (ct.IsCancellationRequested) return;

                _pendingResumeMs = (int)(savedPos * 1000.0);
                _mediaPlayer.OpenMedia(
                    MediaPathType.AbsolutePathOrURL, stream.StreamUrl, autoPlay: false);

                // Reschedule for the new expiry
                _expiryChecker.Start(stream.ExpiresAt, async () =>
                {
                    await RefreshStreamUrlAsync(ct);
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayVideoPage] Stream refresh failed: {e.Message}");
            }
        }

        // ── AVPro events ──────────────────────────────────────────────

        private void OnMediaPlayerEvent(
            MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
        {
            switch (eventType)
            {
                case MediaPlayerEvent.EventType.MetaDataReady:
                    OnMetaDataReady();
                    break;

                case MediaPlayerEvent.EventType.FinishedPlaying:
                    OnVideoFinished();
                    break;
            }
        }

        private void OnMetaDataReady()
        {
            // Apply the current playback speed to the freshly opened media
            _mediaPlayer.PlaybackRate = _playbackRate;

            if (_pendingResumeMs > 0)
            {
                double seekSecs = _pendingResumeMs / 1000.0;

                // Prefer AVPro's reported duration; fall back to the API value when AVPro
                // hasn't resolved it yet (returns 0 on some streams at MetaDataReady).
                double duration = _mediaPlayer.Info?.GetDuration() ?? 0;
                if (duration <= 0 && _currentItem != null && _currentItem.DurationMs > 0)
                    duration = _currentItem.DurationMs / 1000.0;

                // If duration is known and the saved position is in the last 5%,
                // treat the episode as completed and play from the beginning.
                bool isCompleted = duration > 0 && seekSecs >= duration * 0.95;
                if (!isCompleted)
                    _mediaPlayer.Control?.Seek(seekSecs);

                _pendingResumeMs = 0;
            }

            _mediaPlayer.Control?.Play();
        }

        private void OnVideoFinished()
        {
            if (!DevFlags.UseFakeData)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            var state = AppPlaybackState.Instance;
            if (!state.HasNext)
            {
                // 影片播放結束（且無下一支）：仍停在播放頁，不是切頁，
                // 故由本元件自帶 fade out/in 還原 3D 場景與天空盒
                CinemaModeController.Instance?.Exit(fade: true);
                ShowControls(); // surface controls so the user can navigate
                return; // Stay on the last frame; user presses Home
            }

            state.AdvancePlaylist();
            _currentItem = state.Playlist[state.PlaylistIndex];
            _knownProgressUpdatedAt = ProgressUpdatedAt(_currentItem);
            RefreshNavButtons();

            if (_cts == null || _cts.IsCancellationRequested)
                _cts = new CancellationTokenSource();

            LoadAndPlayAsync(_projectId, _currentItem, _cts.Token).Forget();
        }

        // ── Playback controls ─────────────────────────────────────────

        private void OnHomeClicked() => UIManager.Instance.GoBack(fade: true);

        private void OnPlayPauseClicked()
        {
            if (_mediaPlayer?.Control == null) return;
            if (_mediaPlayer.Control.IsPlaying())
                _mediaPlayer.Control.Pause();
            else
                _mediaPlayer.Control.Play();
        }

        private void OnSkipForward()
        {
            if (_mediaPlayer?.Control == null) return;
            double duration = _mediaPlayer.Info?.GetDuration() ?? 0;
            double target = Math.Min(_mediaPlayer.Control.GetCurrentTime() + SkipSeconds, duration);
            _mediaPlayer.Control.Seek(target);
        }

        private void OnSkipBackward()
        {
            if (_mediaPlayer?.Control == null) return;
            double target = Math.Max(_mediaPlayer.Control.GetCurrentTime() - SkipSeconds, 0.0);
            _mediaPlayer.Control.Seek(target);
        }

        private void OnNextClicked()
        {
            var state = AppPlaybackState.Instance;
            if (!state.HasNext) return;

            if (!DevFlags.UseFakeData)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            state.AdvancePlaylist();
            SwitchToPlaylistItem(state.Playlist[state.PlaylistIndex]);
        }

        private void OnPrevClicked()
        {
            var state = AppPlaybackState.Instance;
            if (!state.HasPrev) return;

            if (!DevFlags.UseFakeData)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            state.RewindPlaylist();
            SwitchToPlaylistItem(state.Playlist[state.PlaylistIndex]);
        }

        private void SwitchToPlaylistItem(PlaylistItem item)
        {
            _expiryChecker.Stop();
            _currentItem = item;
            _knownProgressUpdatedAt = ProgressUpdatedAt(item);
            RefreshNavButtons();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            LoadAndPlayAsync(_projectId, _currentItem, _cts.Token).Forget();
        }

        private void OnSliderChanged(float value)
        {
            if (_preventSliderCallback || _mediaPlayer?.Control == null) return;
            // Scrubbing is activity — keep controls alive through a long drag.
            _controlsIdleTimer = 0f;
            double duration = _mediaPlayer.Info?.GetDuration() ?? 0;
            if (duration > 0)
                _mediaPlayer.Control.Seek(value * duration);
        }

        // ── Speed ─────────────────────────────────────────────────────

        private void SetSpeed(float rate)
        {
            _playbackRate = rate;

            // Apply to player only if media is open; otherwise applied in OnMetaDataReady
            if (_mediaPlayer != null && _mediaPlayer.MediaOpened)
                _mediaPlayer.PlaybackRate = rate;

            // Update label  — format: "Speed (1x)", "Speed (1.25x)", "Speed (1.5x)"
            if (_speedText != null)
            {
                string label = Mathf.Approximately(rate, 1f)
                    ? "1"
                    : rate.ToString("0.##");
                _speedText.text = $"Speed ({label}x)";
            }

            // Sync toggle visuals without re-firing listeners
            _speed1Toggle?.SetIsOnWithoutNotify(Mathf.Approximately(rate, 1f));
            _speed125Toggle?.SetIsOnWithoutNotify(Mathf.Approximately(rate, 1.25f));
            _speed15Toggle?.SetIsOnWithoutNotify(Mathf.Approximately(rate, 1.5f));
        }

        // ── Watch history ─────────────────────────────────────────────

        private async UniTaskVoid SyncWatchProgressAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_projectId) || _currentItem == null) return;

            var request = new UpdateWatchProgressRequest
            {
                ProgressMs = (int)AppPlaybackState.Instance.CurrentPositionMs,
                ProjectId = _projectId,
                KnownProgressUpdatedAt = _knownProgressUpdatedAt,
            };

            try
            {
                var (record, _) = await ServiceLocator.Instance.V2.WatchHistory
                    .UpdateProgressAsync(_currentItem.VideoId, request, ct);
                if (record != null)
                {
                    _knownProgressUpdatedAt = record.ProgressUpdatedAt;
                    ProjectCacheRepository.Instance.UpsertProgress(record);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayVideoPage] Watch progress sync failed: {e.Message}");
            }
        }

        private static string ProgressUpdatedAt(PlaylistItem item) =>
            item == null ? null : ProjectCacheRepository.Instance.GetProgressUpdatedAt(item.VideoId);

        // ── Controls visibility ───────────────────────────────────────

        private bool ControlsVisible => _controlsGroup != null && _controlsGroup.alpha > 0f;

        private void OnShowControlsInput(InputAction.CallbackContext _) => ShowControls();

        /// <summary>
        /// Re-summons the controls from a controller index-trigger press or a
        /// hand-tracking index-finger pinch (thumb + index touch). Polled in Update
        /// while the page is active.
        /// </summary>
        private void PollShowControlsInput()
        {
            if (!_showControlsInputActive) return;

            // Controllers — index trigger on either hand. Use RawButton (not the
            // Primary/Secondary virtual buttons): the virtual buttons are bound to the
            // "active" controller, so filtering them by LTouch/RTouch can drop the press.
            if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger) ||
                OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
            {
                ShowControls();
                return;
            }

            // Hand tracking — rising edge of an index-finger pinch, so holding the
            // pinch doesn't re-fire every frame.
            if (PollPinch(_leftHand, ref _leftPinching) || PollPinch(_rightHand, ref _rightPinching))
                ShowControls();
        }

        /// <summary>Returns true on the frame an index-finger pinch begins.</summary>
        private static bool PollPinch(OVRHand hand, ref bool wasPinching)
        {
            bool pinching = hand != null
                         && hand.IsTracked
                         && hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            bool justStarted = pinching && !wasPinching;
            wasPinching = pinching;
            return justStarted;
        }

        // The Activate actions are shared with XRI, so we only attach/detach our
        // callback here — never Disable() them, or the trigger would stop working
        // app-wide once this page is left. We Enable() defensively in case the rig
        // hasn't, which is idempotent if it already has.
        private void SubscribeShowControls()
        {
            // Enable OVRHand pinch polling + the controller trigger action for the
            // lifetime of the page.
            _showControlsInputActive = true;
            _leftPinching = _rightPinching = false;
            _triggerShowAction?.Enable();

            if (_showControlsActions == null) return;
            foreach (var reference in _showControlsActions)
            {
                var action = reference != null ? reference.action : null;
                if (action == null) continue;
                action.performed += OnShowControlsInput;
                action.Enable();
            }
        }

        private void UnsubscribeShowControls()
        {
            _showControlsInputActive = false;
            _triggerShowAction?.Disable();

            if (_showControlsActions == null) return;
            foreach (var reference in _showControlsActions)
            {
                var action = reference != null ? reference.action : null;
                if (action == null) continue;
                action.performed -= OnShowControlsInput;
            }
        }

        /// <summary>Shows the controls and restarts the inactivity countdown.</summary>
        private void ShowControls()
        {
            _controlsIdleTimer = 0f;
            if (_controlsGroup == null) return;
            _controlsGroup.alpha = 1f;
            _controlsGroup.interactable = true;
            _controlsGroup.blocksRaycasts = true;
        }

        /// <summary>Hides the controls (and the speed sub-panel) without disabling the
        /// GameObject, so this handler's Update and input callbacks keep running.</summary>
        private void HideControls()
        {
            _speedPanel?.SetActive(false);
            if (_controlsGroup == null) return;
            _controlsGroup.alpha = 0f;
            _controlsGroup.interactable = false;
            _controlsGroup.blocksRaycasts = false;
        }

        // ── UI helpers ────────────────────────────────────────────────

        private void RefreshPlayPauseButton()
        {
            if (_playPauseIcon == null) return;
            bool playing = _mediaPlayer?.Control?.IsPlaying() ?? false;
            var target = playing ? _pauseSprite : _playSprite;
            if (target != null) _playPauseIcon.sprite = target;
        }

        private void RefreshNavButtons()
        {
            var state = AppPlaybackState.Instance;
            if (_nextButton != null) _nextButton.interactable = state.HasNext;
            if (_prevButton != null) _prevButton.interactable = state.HasPrev;
        }

        private void UpdateTimeText(double current, double duration)
        {
            if (_currentTimeText != null) _currentTimeText.text = FormatTime(current);
            if (_durationText != null) _durationText.text = FormatTime(duration);
        }

        private static string FormatTime(double totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            var t = TimeSpan.FromSeconds(totalSeconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }
    }
}
