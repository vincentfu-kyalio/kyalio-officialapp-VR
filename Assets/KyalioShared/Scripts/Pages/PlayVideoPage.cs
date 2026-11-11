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
    public class PlayVideoPage : MonoBehaviour, IPageHandler, IRevealGate
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
        [Tooltip("When on, the current video repeats instead of ending / advancing the playlist.")]
        [SerializeField] private Toggle _loopToggle;

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

        // Reveal gate: the fade into this page stays black until the opened media shows
        // its first frame at the resume position, so the user never sees an empty sphere
        // and then the video popping in, or frame 0 jumping to the saved position.
        private bool _firstFrameReady;
        private bool _awaitingResumeSeek;
        private float _gateStartedAt;

        // True once CurrentPositionMs holds this video's real position (see Update). Until
        // then nothing is synced, so backing out early keeps the saved resume point.
        private bool _positionValid;

        // Fallback if a platform never reports FirstFrameReady / FinishedSeeking: once
        // playing this long, treat the media as settled so the scrub bar is not frozen.
        private const float GateFallbackSeconds = 3f;

        /// <inheritdoc/>
        public bool IsReadyToReveal => _firstFrameReady && !_awaitingResumeSeek;

        // Nothing will play (no media, failed stream, player error): reveal the controls.
        private void RevealNow()
        {
            _firstFrameReady    = true;
            _awaitingResumeSeek = false;
        }

        /// <summary>
        /// Puts the scrub bar and time labels where this video will start (its saved
        /// position, or 0 when it was nearly finished — same rule as OnMetaDataReady)
        /// before the media opens, so they never show the previous video's values.
        /// </summary>
        private void ShowStartPosition(PlaylistItem item)
        {
            double duration = (item?.DurationMs ?? 0) / 1000.0;
            double start = item != null && !string.IsNullOrEmpty(item.VideoId)
                ? ProjectCacheRepository.Instance.GetProgressMs(item.VideoId) / 1000.0
                : 0;
            if (duration <= 0 || start >= duration * 0.95) start = 0;

            if (_progressSlider != null)
                _progressSlider.SetValueWithoutNotify(duration > 0 ? (float)(start / duration) : 0f);
            UpdateTimeText(start, duration);
        }
        private bool _preventSliderCallback;

        // Last values pushed to the controls. The UI is rendered through the
        // OVROverlayCanvas imposter, which redraws the whole canvas whenever anything
        // on it changes, so Update only touches the UI when the visible value changes.
        private int _shownCurrentSecond = -1;
        private int _shownDurationSecond = -1;

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
            _loopToggle?.onValueChanged.AddListener(SetLoop);

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

            // While the media is still opening it reports the previous video's time,
            // then 0, then the resume point; keep showing the start position set by
            // ShowStartPosition until playback has settled there.
            if (!IsReadyToReveal && _mediaPlayer.Control.IsPlaying() &&
                Time.unscaledTime - _gateStartedAt > GateFallbackSeconds)
                RevealNow();
            bool settled = IsReadyToReveal;

            // Scrub bar — guard against re-entrant callbacks
            if (settled && duration > 0 && !_preventSliderCallback)
            {
                _preventSliderCallback = true;
                UpdateProgressSlider((float)(current / duration));
                _preventSliderCallback = false;
            }

            if (settled)
            {
                UpdateTimeText(current, duration);
                // Only a settled player reports this video's real position; before that it
                // is the previous video's time or 0, which must never be saved as progress.
                AppPlaybackState.Instance.CurrentPositionMs = (long)(current * 1000.0);
                _positionValid = true;
                Session.PlaybackActive = true;   // from now on a token expiry waits for the viewer
            }
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
            if (_mediaPlayer.Control.IsPlaying() && _positionValid && !DevFlags.UseFakeData)
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
            // Keep the user's Loop choice from the toggle; it persists across
            // episodes and media re-opens (AVPro stores it on the MediaPlayer).
            SetLoop(_loopToggle != null && _loopToggle.isOn);

            LoadAndPlayAsync(_projectId, _currentItem, _cts.Token).Forget();
        }

        public void OnExit()
        {
            UnsubscribeShowControls();
            _expiryChecker.Stop();
            _cts?.Cancel();

            // Save final progress with a fresh token — page is closing but the request
            // must be allowed to complete.
            if (!DevFlags.UseFakeData && _currentItem != null && _positionValid)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            AppState.Instance.MarkWatchHistoryDirty();

            // 回到主頁面時還原 3D 場景與天空盒（切頁 fade 由 UIManager.GoBack(fade: true) 負責，
            // 故即時切換；影片自然結束時不會還原，統一在這裡還原）
            CinemaModeController.Instance?.Exit(fade: false);

            _mediaPlayer?.Control?.Pause();
            _mediaPlayer?.CloseMedia();

            if (_avProRoot != null) _avProRoot.SetActive(false);
            if (_tabBarRoot != null) _tabBarRoot.SetActive(true);

            // Restore the Meta composition-layer rendering for the menu UI.
            if (_overlayCanvas != null) _overlayCanvas.overlayEnabled = true;

            Session.PlaybackActive = false;
            // Catch-all: the player was left with the token expiry still deferred, by a
            // route that did not complete it (Home does). Sign out on the next frame —
            // OnExit runs inside a navigation, which must finish before another starts.
            if (Session.ExpiryDeferred) CompleteDeferredExpiryNextFrameAsync().Forget();
        }

        private static async UniTaskVoid CompleteDeferredExpiryNextFrameAsync()
        {
            await UniTask.Yield();
            Session.CompleteDeferredExpiry();
        }

        // ── Load & play ───────────────────────────────────────────────

        private async UniTaskVoid LoadAndPlayAsync(
            string projectId, PlaylistItem item, CancellationToken ct)
        {
            // Every media open (page entry, auto-advance, Next / Prev) comes through here:
            // reset the reveal gate and the position guard, and show where this video will
            // start, before the player reports anything for it.
            _firstFrameReady    = false;
            _awaitingResumeSeek = false;
            _gateStartedAt      = Time.unscaledTime;
            _positionValid      = false;
            Session.PlaybackActive = false;   // until this video plays, an expiry signs out at once
            ShowStartPosition(item);

            if (string.IsNullOrEmpty(item?.VideoId)) { RevealNow(); return; }

            int resumeMs = ProjectCacheRepository.Instance.GetProgressMs(item.VideoId);

            // 1. Prefer locally downloaded file
            var localPath = DownloadedVideoState.Instance
                .GetFilePath(projectId, item.VideoId);

            if (localPath != null)
            {
                OpenMedia(localPath, resumeMs, fileOffset: Kyalio.Services.DownloadStorage.HeaderSize);
                return;
            }

            // 2. Dev mode — no API available; fall back to the dev test video
            if (DevFlags.UseFakeData)
            {
                OpenDevVideo(resumeMs);
                return;
            }

            // 3. Fetch stream URL from API — needs a valid token. With the expiry already
            //    deferred (auto-advance / Next / Prev to a streamed episode), sign out now
            //    rather than leave the viewer on an empty player. A downloaded episode played
            //    above without one.
            if (Session.CompleteDeferredExpiry()) return;

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
                RevealNow();
            }
        }

        /// <summary>
        /// Opens media at the given path and stores the resume position. fileOffset skips the
        /// obfuscation header of a downloaded file (Android only); every other source passes 0,
        /// which also clears an offset left by a previously played download.
        /// </summary>
        private void OpenMedia(string path, int resumeMs,
            MediaPathType pathType = MediaPathType.AbsolutePathOrURL, int fileOffset = 0)
        {
            _pendingResumeMs = resumeMs;
            _mediaPlayer.PlatformOptionsAndroid.fileOffset = fileOffset;
            _mediaPlayer.OpenMedia(pathType, path, autoPlay: false);
        }

        /// <summary>
        /// Fake-data mode has no stream API, so play DevFlags.DevVideoPath instead,
        /// letting the controls be exercised in the Editor without signing in.
        /// </summary>
        private void OpenDevVideo(int resumeMs)
        {
            string path = DevFlags.DevVideoPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning(
                    "[PlayVideoPage] No local download and no dev video path; nothing to play.");
                RevealNow();
                return;
            }

            // Let AVPro resolve StreamingAssets itself (Android's path is inside the APK).
            bool absolute = path.Contains("://") || System.IO.Path.IsPathRooted(path);
            OpenMedia(path, resumeMs, absolute
                ? MediaPathType.AbsolutePathOrURL
                : MediaPathType.RelativeToStreamingAssetsFolder);
        }

        private async UniTask RefreshStreamUrlAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested || _currentItem == null) return;
            // Token expired: the refresh would be rejected. Let the current URL play to its
            // end; the player error when it expires completes the sign-out (Error event).
            if (Session.ExpiryDeferred) return;

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

                case MediaPlayerEvent.EventType.FirstFrameReady:
                    _firstFrameReady = true;
                    break;

                case MediaPlayerEvent.EventType.FinishedSeeking:
                    _awaitingResumeSeek = false;
                    break;

                case MediaPlayerEvent.EventType.Error:
                    // With the token expired, a playback error (typically the stream URL
                    // running out) cannot be recovered: sign out instead of freezing.
                    if (Session.CompleteDeferredExpiry()) break;
                    RevealNow();
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
                if (!isCompleted && _mediaPlayer.Control != null)
                {
                    _awaitingResumeSeek = true;
                    _mediaPlayer.Control.Seek(seekSecs);
                }

                _pendingResumeMs = 0;
            }

            _mediaPlayer.Control?.Play();
        }

        private void OnVideoFinished()
        {
            if (!DevFlags.UseFakeData)
                SyncWatchProgressAsync(CancellationToken.None).Forget();

            // AVPro loops natively when Loop is set, so this normally doesn't fire.
            // Fallback for platforms/streams that still report the end: restart.
            if (_mediaPlayer != null && _mediaPlayer.Loop)
            {
                _mediaPlayer.Control?.Seek(0);
                _mediaPlayer.Control?.Play();
                return;
            }

            var state = AppPlaybackState.Instance;
            if (!state.HasNext)
            {
                // 影片播放結束（且無下一支）：維持影院模式，畫面停在最後一格，
                // 不還原 3D 場景；按 Home 離開時由 OnExit 還原。
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

        private void OnHomeClicked()
        {
            // The token expired while watching: sign out now instead of returning to the menu.
            if (Session.CompleteDeferredExpiry()) return;
            UIManager.Instance.GoBack(fade: true);
        }

        private void OnPlayPauseClicked()
        {
            if (_mediaPlayer?.Control == null) return;
            if (_mediaPlayer.Control.IsPlaying())
            {
                _mediaPlayer.Control.Pause();
                return;
            }

            // Ended on the last frame — Play replays from the start.
            if (_mediaPlayer.Control.IsFinished())
                _mediaPlayer.Control.Seek(0);
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

        // ── Loop ──────────────────────────────────────────────────────

        private void SetLoop(bool loop)
        {
            if (_mediaPlayer != null) _mediaPlayer.Loop = loop;
            _loopToggle?.SetIsOnWithoutNotify(loop);
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
            // Before the player settles, CurrentPositionMs is the previous video's time or 0
            // (Next / Prev / auto-advance can call this then): never record it as progress.
            if (!_positionValid) return;

            int progressMs = (int)AppPlaybackState.Instance.CurrentPositionMs;

            // Cache the position now: the page shown on leaving the player renders before
            // the PATCH below returns and would otherwise show the previous progress.
            ProjectCacheRepository.Instance.SetLocalProgress(
                _projectId, _currentItem.VideoId, progressMs);

            if (Session.ExpiryDeferred) return;   // token expired: the server would reject it

            var request = new UpdateWatchProgressRequest
            {
                ProgressMs = progressMs,
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

        /// <summary>
        /// Moves the scrub bar only once the change is at least a pixel wide: a long video
        /// advances a fraction of a pixel per frame, and each change redraws the canvas.
        /// </summary>
        private void UpdateProgressSlider(float value)
        {
            if (_progressSlider == null) return;
            var width = ((RectTransform)_progressSlider.transform).rect.width;
            if (Mathf.Abs(value - _progressSlider.value) * width < 1f) return;
            _progressSlider.SetValueWithoutNotify(value);
        }

        /// <summary>Rewrites the time labels only when the displayed second changes (no per-frame string garbage).</summary>
        private void UpdateTimeText(double current, double duration)
        {
            var currentSecond = (int)Math.Max(0, current);
            var durationSecond = (int)Math.Max(0, duration);

            if (currentSecond != _shownCurrentSecond && _currentTimeText != null)
            {
                _shownCurrentSecond = currentSecond;
                _currentTimeText.text = FormatTime(currentSecond);
            }
            if (durationSecond != _shownDurationSecond && _durationText != null)
            {
                _shownDurationSecond = durationSecond;
                _durationText.text = FormatTime(durationSecond);
            }
        }

        private static string FormatTime(int totalSeconds)
        {
            var t = TimeSpan.FromSeconds(totalSeconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }
    }
}
