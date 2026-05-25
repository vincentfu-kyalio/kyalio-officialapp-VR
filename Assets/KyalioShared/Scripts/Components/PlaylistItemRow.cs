using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Services;
using Kyalio.State;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// A single row item for the Playlist.
    /// Attach to the PlaylistItemRow prefab.
    /// Inspector: thumbnail, titleText, durationText, playButton,
    ///            downloadButton, downloadProgressSlider, downloadStatusImage,
    ///            downloadSprite, queuedSprite(optional), completeSprite
    /// </summary>
    public class PlaylistItemRow : MonoBehaviour
    {
        // ── Basic Fields ──────────────────────────────────────────────
        [SerializeField] private Image thumbnail;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private Button playButton;

        // ── Watch Progress UI ─────────────────────────────────────────
        [SerializeField] private Slider watchProgressSlider;

        // ── Download UI ───────────────────────────────────────────────
        [SerializeField] private Button downloadButton;
        [SerializeField] private Slider downloadProgressSlider;
        [SerializeField] private Image downloadStatusImage;
        [SerializeField] private Sprite downloadSprite;
        [SerializeField] private Sprite queuedSprite;    // Optional; falls back to downloadSprite when null
        [SerializeField] private Sprite completeSprite;

        // ── Internal State ────────────────────────────────────────────
        private PlaylistItem _item;
        private string _projectId;
        private CancellationTokenSource _cts;
        private bool _downloadEventsSubscribed;

        // ── Callbacks (injected by ProjectInfoPage) ───────────────────
        public System.Action<PlaylistItem> OnClicked;

        /// <summary>
        /// Shows a confirmation dialog (message, onYes, onCancel).
        /// Injected by ProjectInfoPage; used for delete confirmation and mobile data confirmation.
        /// </summary>
        public System.Action<string, System.Action, System.Action> OnConfirmRequested;

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            playButton.onClick.AddListener(() => OnClicked?.Invoke(_item));

            if (downloadButton != null)
                downloadButton.onClick.AddListener(OnDownloadButtonClicked);
        }

        private void OnDownloadButtonClicked()
        {
            HandleDownloadClickAsync().Forget();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            UnsubscribeDownloadEvents();
        }

        // ── Bind ──────────────────────────────────────────────────────
        /// <summary>Bind must be called after OnClicked / OnConfirmRequested have been set.</summary>
        public void Bind(PlaylistItem item, string projectId)
        {
            _item      = item;
            _projectId = projectId;

            titleText.text       = item.Title ?? string.Empty;
            descriptionText.text = item.Description;

            if (durationText != null)
                durationText.text = DurationFormatter.Format((int?)item.DurationMs);

            if (sizeText != null)
            {
                sizeText.gameObject.SetActive(item.SizeBytes > 0);
                if (item.SizeBytes > 0)
                    sizeText.text = FormatSize(item.SizeBytes);
            }

            if (watchProgressSlider != null)
            {
                int progressMs = ProjectCacheRepository.Instance.GetProgressMs(item.VideoId);
                bool hasProgress = progressMs > 0 && item.DurationMs > 0;
                watchProgressSlider.gameObject.SetActive(hasProgress);
                if (hasProgress)
                    watchProgressSlider.value = Mathf.Clamp01((float)progressMs / item.DurationMs);
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            if (!string.IsNullOrEmpty(item.ThumbnailUrl))
                LoadThumbnailAsync(ThumbnailLoader.Resolve(item.ThumbnailUrl), _cts.Token).Forget();

            // Subscribe events during Bind to ensure _item / _projectId are already set
            SubscribeDownloadEvents();
            RefreshDownloadUI();
        }

        // ── Event Subscription Management ────────────────────────────
        private void SubscribeDownloadEvents()
        {
            var dm = DownloadManager.Instance;
            if (dm == null)
            {
                Debug.LogWarning("[PlaylistItemRow] DownloadManager.Instance is null — " +
                                 "DownloadManager component missing on AppManager?");
                return;
            }

            UnsubscribeDownloadEvents(); // Prevent duplicate subscriptions

            dm.OnProgress     += HandleProgress;
            dm.OnCompleted    += HandleCompleted;
            dm.OnFailed       += HandleFailed;
            dm.OnCancelled    += HandleCancelled;
            dm.OnQueueChanged += RefreshDownloadUI;
            DownloadedVideoState.OnRecordsChanged += RefreshDownloadUI;
            _downloadEventsSubscribed = true;
        }

        private void UnsubscribeDownloadEvents()
        {
            if (!_downloadEventsSubscribed) return;
            var dm = DownloadManager.Instance;
            if (dm == null) return;
            dm.OnProgress     -= HandleProgress;
            dm.OnCompleted    -= HandleCompleted;
            dm.OnFailed       -= HandleFailed;
            dm.OnCancelled    -= HandleCancelled;
            dm.OnQueueChanged -= RefreshDownloadUI;
            DownloadedVideoState.OnRecordsChanged -= RefreshDownloadUI;
            _downloadEventsSubscribed = false;
        }

        // ── Download UI State Machine ──────────────────────────────────
        private enum DownloadUIState { Idle, Queued, Downloading, Downloaded }

        private DownloadUIState GetCurrentState()
        {
            if (_item == null || _projectId == null) return DownloadUIState.Idle;

            // HasDownload takes priority: AddRecord is written before OnCompleted fires,
            // at which point _current is still non-null, causing IsDownloading to return a false positive.
            // Confirm the local file exists first.
            if (DownloadedVideoState.Instance.HasDownload(_projectId, _item.VideoId))
                return DownloadUIState.Downloaded;

            var dm = DownloadManager.Instance;
            if (dm != null)
            {
                if (dm.IsDownloading(_projectId, _item.VideoId)) return DownloadUIState.Downloading;
                if (dm.IsQueued(_projectId, _item.VideoId))      return DownloadUIState.Queued;
            }

            return DownloadUIState.Idle;
        }

        private void RefreshDownloadUI()
        {
            if (downloadButton == null) return;

            var state      = GetCurrentState();
            bool showButton = true;
            bool showStatus = state != DownloadUIState.Downloading;

            downloadButton.gameObject.SetActive(showButton);

            if (downloadProgressSlider != null)
                downloadProgressSlider.gameObject.SetActive(state == DownloadUIState.Downloading);

            if (downloadStatusImage != null)
            {
                downloadStatusImage.gameObject.SetActive(showStatus);
                if (!showStatus) return;

                downloadStatusImage.sprite = state switch
                {
                    DownloadUIState.Downloaded => completeSprite,
                    DownloadUIState.Queued     => queuedSprite != null ? queuedSprite : downloadSprite,
                    _                          => downloadSprite
                };
            }
        }

        // ── Event Handlers ────────────────────────────────────────────
        private void HandleProgress(string projectId, string videoId, float progress)
        {
            if (!IsFor(projectId, videoId)) return;
            if (downloadProgressSlider != null)
                downloadProgressSlider.value = progress;
            RefreshDownloadUI();
        }

        private void HandleCompleted(string projectId, string videoId, string _)
        {
            if (!IsFor(projectId, videoId)) return;
            RefreshDownloadUI();
        }

        private void HandleFailed(string projectId, string videoId, string error)
        {
            if (!IsFor(projectId, videoId)) return;
            Debug.LogWarning($"[PlaylistItemRow] Download failed: {error}");
            RefreshDownloadUI();
        }

        private void HandleCancelled(string projectId, string videoId)
        {
            if (!IsFor(projectId, videoId)) return;
            RefreshDownloadUI();
        }

        private bool IsFor(string projectId, string videoId) =>
            projectId == _projectId && videoId == _item?.VideoId;

        // ── Click Handler ─────────────────────────────────────────────
        private async UniTask HandleDownloadClickAsync()
        {
            Debug.Log($"[PlaylistItemRow] Download button clicked. item={_item?.VideoId} projectId={_projectId}");

            if (_item == null || string.IsNullOrEmpty(_projectId))
            {
                Debug.LogWarning("[PlaylistItemRow] _item or _projectId is null, aborting");
                return;
            }

            if (DownloadManager.Instance == null)
            {
                Debug.LogError("[PlaylistItemRow] DownloadManager.Instance is null! " +
                               "Add DownloadManager component to AppManager GameObject.");
                return;
            }

            var state = GetCurrentState();
            Debug.Log($"[PlaylistItemRow] Current download state: {state}");

            if (state == DownloadUIState.Downloaded)
            {
                bool confirmed = false;
                var tcs = new UniTaskCompletionSource();
                OnConfirmRequested?.Invoke(
                    "Delete the downloaded video?",
                    () => { confirmed = true; tcs.TrySetResult(); },
                    () => tcs.TrySetResult()
                );
                if (OnConfirmRequested == null)
                {
                    Debug.LogWarning("[PlaylistItemRow] OnConfirmRequested not set, delete aborted");
                    return;
                }
                await tcs.Task;
                if (!confirmed) return;

                DownloadManager.Instance.Delete(_projectId, _item.VideoId);
                RefreshDownloadUI();
                return;
            }

            if (state == DownloadUIState.Queued)
            {
                Debug.Log("[PlaylistItemRow] Cancelling queued download");
                DownloadManager.Instance.Cancel(_projectId, _item.VideoId);
                return;
            }

            if (state == DownloadUIState.Downloading)
            {
                bool confirmed = false;
                var tcs = new UniTaskCompletionSource();
                OnConfirmRequested?.Invoke(
                    "Cancel this download?",
                    () => { confirmed = true; tcs.TrySetResult(); },
                    () => tcs.TrySetResult()
                );
                if (OnConfirmRequested == null)
                {
                    Debug.LogWarning("[PlaylistItemRow] OnConfirmRequested not set, cancel aborted");
                    return;
                }
                await tcs.Task;
                if (!confirmed) return;

                DownloadManager.Instance.Cancel(_projectId, _item.VideoId);
                return;
            }

            if (state == DownloadUIState.Idle)
            {
                var reachability = Application.internetReachability;
                Debug.Log($"[PlaylistItemRow] Network reachability: {reachability}");

                if (reachability == NetworkReachability.NotReachable)
                {
                    Debug.LogWarning("[PlaylistItemRow] No network connection");
                    return;
                }

                if (reachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                {
                    var sizeStr = _item.SizeBytes > 0
                        ? FormatSize(_item.SizeBytes)
                        : "unknown size";
                    bool confirmed = false;
                    var tcs = new UniTaskCompletionSource();
                    OnConfirmRequested?.Invoke(
                        $"You're on mobile data. This video is {sizeStr}. Download anyway?",
                        () => { confirmed = true; tcs.TrySetResult(); },
                        () => tcs.TrySetResult()
                    );
                    if (OnConfirmRequested == null) tcs.TrySetResult();
                    await tcs.Task;
                    if (!confirmed) return;
                }

                Debug.Log($"[PlaylistItemRow] Enqueuing download: {_projectId}/{_item.VideoId}");
                DownloadManager.Instance.Enqueue(_projectId, _item.VideoId, _item.SizeBytes);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} <size=80%>GB</size>";
            if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F0} <size=80%>MB</size>";
            if (bytes >= 1_024)         return $"{bytes / 1024.0:F0} <size=80%>KB</size>";
            return $"{bytes} <size=80%>B</size>";
        }

        private async UniTaskVoid LoadThumbnailAsync(string url, CancellationToken ct)
        {
            var sprite = await ThumbnailLoader.LoadAsync(url, ct);
            if (sprite != null && !ct.IsCancellationRequested)
            {
                thumbnail.sprite = sprite;
                thumbnail.color  = Color.white;
            }
        }
    }
}
