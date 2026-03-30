using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.Repositories;
using Kyalio.Services;
using Kyalio.State;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Video / programme detail page.
    /// param: string projectId
    /// </summary>
    public class ProjectInfoPage : MonoBehaviour, IPageHandler
    {
        [Header("Header")]
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Button backButton;
        [SerializeField] private Button favoriteButton;
        [SerializeField] private Image favoriteIcon;
        [SerializeField] private Sprite favoriteActiveSprite;
        [SerializeField] private Sprite favoriteInactiveSprite;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI contributorText;
        [SerializeField] private TextMeshProUGUI programText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private TextMeshProUGUI videoTypeText;
        [SerializeField] private TextMeshProUGUI sizeText;

        [Header("Playlist")]
        [SerializeField] private Transform playlistContainer;
        [SerializeField] private PlaylistItemRow playlistItemPrefab;
        [SerializeField] private TextMeshProUGUI playlistCountText;

        [Header("Progress")]
        [SerializeField] private Slider overallProgressSlider;

        [Header("Actions")]
        [SerializeField] private Button playAllButton;

        [Header("Download All")]
        [SerializeField] private Button downloadAllButton;
        [SerializeField] private Slider downloadAllProgressSlider;
        [SerializeField] private Image downloadAllStatusImage;
        [SerializeField] private Sprite downloadAllDownloadSprite;
        [SerializeField] private Sprite downloadAllCompleteSprite;

        // Confirm Dialog (shared)
        [SerializeField] private GameObject confirmDialog;
        [SerializeField] private Button dialogYesButton;
        [SerializeField] private Button dialogCancelButton;
        [SerializeField] private TextMeshProUGUI dialogMessageText;

        private string _projectId;
        private string _entrySource;
        private string _sourceSearchEventId;
        private System.DateTime _startedAt;
        private bool _videoStarted;
        private ProjectDetail _currentDetail;
        private CancellationTokenSource _cts;
        private System.Action _dialogYesAction;
        private System.Action _dialogNoAction;

        private void OnValidate()
        {
            if (backButton == null) Debug.LogWarning("[ProjectInfoPage] backButton is not assigned.", this);
            if (playlistContainer == null) Debug.LogWarning("[ProjectInfoPage] playlistContainer is not assigned.", this);
            if (playlistItemPrefab == null) Debug.LogWarning("[ProjectInfoPage] playlistItemPrefab is not assigned.", this);
            if (downloadAllButton != null && downloadAllProgressSlider == null)
                Debug.LogWarning("[ProjectInfoPage] downloadAllProgressSlider is not assigned.", this);
        }

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            backButton.onClick.AddListener(() => UIManager.Instance.GoBack());
            favoriteButton.onClick.AddListener(OnFavoriteToggled);

            if (playAllButton != null)
                playAllButton.onClick.AddListener(OnPlayAll);
            if (downloadAllButton != null)
                downloadAllButton.onClick.AddListener(() => OnDownloadAllClickAsync().Forget());

            if (dialogYesButton != null)
                dialogYesButton.onClick.AddListener(OnDialogYes);
            if (dialogCancelButton != null)
                dialogCancelButton.onClick.AddListener(OnDialogCancel);
            if (confirmDialog != null)
                confirmDialog.SetActive(false);
        }

        public void OnEnter(object param)
        {
            SubscribeDownloadEvents();

            // Accept either ProjectNavParam (new) or bare string projectId (legacy)
            string incomingId = null;
            if (param is ProjectNavParam nav)
            {
                incomingId            = nav.ProjectId;
                _entrySource          = nav.Source ?? "direct";
                _sourceSearchEventId  = nav.SearchEventId;
            }
            else if (param is string s)
            {
                incomingId           = s;
                _entrySource         = "direct";
                _sourceSearchEventId = null;
            }

            if (!string.IsNullOrEmpty(incomingId))
            {
                // Fresh navigation — load the new project
                _projectId    = incomingId;
                _startedAt    = System.DateTime.UtcNow;
                _videoStarted = false;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                LoadAsync(_projectId, _cts.Token).Forget();
            }
            else if (string.IsNullOrEmpty(_projectId))
            {
                // Returning via GoBack with no prior context — nothing to show
                return;
            }
            // else: returning via GoBack — re-fetch from API to get latest progress
            else
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                LoadAsync(_projectId, _cts.Token).Forget();
            }
        }

        public void OnExit()
        {
            UnsubscribeDownloadEvents();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (confirmDialog != null)
                confirmDialog.SetActive(false);
            _dialogYesAction = null;
            _dialogNoAction  = null;

            ReportPageSessionAsync().Forget();
        }

        private async UniTaskVoid ReportPageSessionAsync()
        {
            if (string.IsNullOrEmpty(_projectId)) return;

            var request = new ProjectPageSessionRequest
            {
                ProjectId           = _projectId,
                Source              = _entrySource ?? "direct",
                StartedAt           = _startedAt.ToString("o"),
                DurationMs          = (long)(System.DateTime.UtcNow - _startedAt).TotalMilliseconds,
                VideoStarted        = _videoStarted,
                SourceSearchEventId = _sourceSearchEventId,
                DeviceType          = DeviceTypeHelper.Get(),
            };

            try
            {
                await ServiceLocator.Instance.AnalyticsService
                    .ReportProjectPageSessionAsync(request, System.Threading.CancellationToken.None);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProjectInfoPage] ReportPageSession failed: {e.Message}");
            }
        }

        // ── Load ──────────────────────────────────────────────────────
        private async UniTaskVoid LoadAsync(string projectId, CancellationToken ct)
        {
            LoadingOverlay.Instance.Show();
            try
            {
                var detail = await ServiceLocator.Instance.ProjectService
                    .GetProjectDetailAsync(projectId, ct);
                if (ct.IsCancellationRequested) return;

                _currentDetail = detail;
                ProjectCacheRepository.Instance.CacheDetail(detail);

                if (titleText != null)       titleText.text       = detail.Name ?? string.Empty;
                if (categoryText != null)    categoryText.text    = detail.CategoryName ?? string.Empty;
                if (contributorText != null) contributorText.text = detail.DrName ?? string.Empty;
                if (programText != null)     programText.text     = detail.ProgramName ?? string.Empty;
                if (descriptionText != null) descriptionText.text = detail.Description ?? string.Empty;

                if (durationText != null)
                    durationText.text = FormatDuration(detail.PlaylistDurationSeconds);
                if (videoTypeText != null)
                    videoTypeText.text = ResolveVideoTypeLabel(detail);
                if (sizeText != null)
                    sizeText.text = FormatTotalSize(detail);

                RefreshFavoriteButton();
                RefreshDownloadAllUI();

                if (thumbnailImage != null && !string.IsNullOrEmpty(detail.ThumbnailUrl))
                {
                    thumbnailImage.sprite = null;
                    thumbnailImage.color  = new Color32(43, 43, 43, 255);
                    var sprite = await Kyalio.Utils.ThumbnailLoader.LoadAsync(detail.ThumbnailUrl, ct);
                    if (sprite != null && !ct.IsCancellationRequested)
                    {
                        thumbnailImage.sprite = sprite;
                        thumbnailImage.color  = Color.white;
                    }
                }

                var playlist = detail.Playlist ?? new System.Collections.Generic.List<PlaylistItem>();

                RefreshPlaylistCountText(playlist.Count);

                ClearPlaylistRows();

                RefreshOverallProgress(playlist);

                foreach (var item in playlist)
                {
                    var row = Instantiate(playlistItemPrefab, playlistContainer);
                    row.OnClicked = playlistItem =>
                    {
                        _videoStarted = true;
                        int idx = playlist.IndexOf(playlistItem);
                        PlaybackState.Instance.SetPlaylist(playlist, idx >= 0 ? idx : 0);
                        UIManager.Instance.GoTo(PageType.PlayVideo,
                            new System.ValueTuple<string, PlaylistItem>(projectId, playlistItem));
                    };
                    row.OnConfirmRequested = (msg, yes, no) => ShowConfirmDialog(msg, yes, no);
                    row.Bind(item, projectId);
                }
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectInfoPage] Load failed: {e.Message}");
            }
            finally
            {
                LoadingOverlay.Instance.Hide();
            }
        }

        // ── Download All — State Machine ──────────────────────────────
        private enum ProjectDownloadState { Idle, Downloading, AllDownloaded }

        private ProjectDownloadState GetProjectDownloadState()
        {
            var playlist = _currentDetail?.Playlist;
            if (playlist == null || playlist.Count == 0) return ProjectDownloadState.Idle;

            // AllDownloaded takes priority: when OnCompleted fires, _current is still non-null,
            // so confirm the local record first to avoid a false Downloading state (same fix as in PlaylistItemRow).
            bool allDownloaded = true;
            foreach (var item in playlist)
            {
                if (string.IsNullOrEmpty(item.MediaVideoId)) continue;
                if (!DownloadedVideoState.Instance.HasDownload(_projectId, item.MediaVideoId))
                { allDownloaded = false; break; }
            }
            if (allDownloaded) return ProjectDownloadState.AllDownloaded;

            var dm = DownloadManager.Instance;
            if (dm != null)
            {
                // Downloading only when every item is either already downloaded or queued/active —
                // a single episode being queued must not flip the Download All button into Downloading state.
                bool allCommitted = true;
                foreach (var item in playlist)
                {
                    if (string.IsNullOrEmpty(item.MediaVideoId)) continue;
                    if (!DownloadedVideoState.Instance.HasDownload(_projectId, item.MediaVideoId) &&
                        !dm.IsActive(_projectId, item.MediaVideoId))
                    { allCommitted = false; break; }
                }
                if (allCommitted) return ProjectDownloadState.Downloading;
            }

            return ProjectDownloadState.Idle;
        }

        private void RefreshDownloadAllUI(float sliderProgress = -1f)
        {
            if (downloadAllButton == null) return;

            var state = GetProjectDownloadState();
            bool isDownloading = state == ProjectDownloadState.Downloading;

            // Button is always visible and clickable; while downloading it acts as cancel.
            downloadAllButton.interactable = true;

            // Downloading: hide status image, show progress slider
            if (downloadAllStatusImage != null)
                downloadAllStatusImage.gameObject.SetActive(!isDownloading);

            if (downloadAllProgressSlider != null)
                downloadAllProgressSlider.gameObject.SetActive(isDownloading);

            // Not downloading: update sprite
            if (downloadAllStatusImage != null && !isDownloading)
            {
                downloadAllStatusImage.sprite = state == ProjectDownloadState.AllDownloaded
                    ? downloadAllCompleteSprite
                    : downloadAllDownloadSprite;
            }

            if (isDownloading && downloadAllProgressSlider != null && sliderProgress >= 0f)
                downloadAllProgressSlider.value = sliderProgress;
        }

        // ── Download All — Events ─────────────────────────────────────
        private void SubscribeDownloadEvents()
        {
            var dm = DownloadManager.Instance;
            if (dm == null) return;
            dm.OnProgress     += OnDMProgress;
            dm.OnCompleted    += OnDMStateChanged;
            dm.OnFailed       += OnDMFailed;
            dm.OnCancelled    += OnDMCancelled;
            dm.OnQueueChanged += OnDMQueueChanged;
            DownloadedVideoState.OnRecordsChanged += OnDMQueueChanged;
        }

        private void UnsubscribeDownloadEvents()
        {
            var dm = DownloadManager.Instance;
            if (dm == null) return;
            dm.OnProgress     -= OnDMProgress;
            dm.OnCompleted    -= OnDMStateChanged;
            dm.OnFailed       -= OnDMFailed;
            dm.OnCancelled    -= OnDMCancelled;
            dm.OnQueueChanged -= OnDMQueueChanged;
            DownloadedVideoState.OnRecordsChanged -= OnDMQueueChanged;
        }

        private void OnDMProgress(string projectId, string videoId, float progress)
        {
            if (projectId != _projectId) return;
            RefreshDownloadAllUI(ComputeOverallProgress(videoId, progress));
        }

        /// <summary>
        /// Overall download progress = (size of completed episodes + downloaded size of current episode) / total size of all episodes.
        /// </summary>
        private float ComputeOverallProgress(string currentVideoId, float currentEpisodeProgress)
        {
            var playlist = _currentDetail?.Playlist;
            if (playlist == null) return currentEpisodeProgress;

            long totalSize      = 0;
            long downloadedSize = 0;

            foreach (var item in playlist)
            {
                if (string.IsNullOrEmpty(item.MediaVideoId)) continue;
                long size = item.SizeBytes ?? 0;
                totalSize += size;

                if (DownloadedVideoState.Instance.HasDownload(_projectId, item.MediaVideoId))
                    downloadedSize += size;
                else if (item.MediaVideoId == currentVideoId)
                    downloadedSize += (long)(size * currentEpisodeProgress);
            }

            return totalSize > 0
                ? Mathf.Clamp01((float)downloadedSize / totalSize)
                : currentEpisodeProgress;
        }

        private void OnDMStateChanged(string projectId, string videoId, string _)
        {
            if (projectId != _projectId) return;
            RefreshDownloadAllUI();
        }

        private void OnDMFailed(string projectId, string videoId, string _)
        {
            if (projectId != _projectId) return;
            RefreshDownloadAllUI();
        }

        private void OnDMCancelled(string projectId, string videoId)
        {
            if (projectId != _projectId) return;
            RefreshDownloadAllUI();
        }

        private void OnDMQueueChanged() => RefreshDownloadAllUI();

        // ── Download All — Click ──────────────────────────────────────
        private async UniTask OnDownloadAllClickAsync()
        {
            var playlist = _currentDetail?.Playlist;
            if (playlist == null || playlist.Count == 0) return;

            var state = GetProjectDownloadState();

            if (state == ProjectDownloadState.AllDownloaded)
            {
                bool confirmed = false;
                var tcs = new UniTaskCompletionSource();
                ShowConfirmDialog(
                    "Delete all downloaded episodes?",
                    () => { confirmed = true; tcs.TrySetResult(); },
                    () => tcs.TrySetResult()
                );
                await tcs.Task;
                if (!confirmed) return;

                foreach (var item in playlist)
                {
                    if (!string.IsNullOrEmpty(item.MediaVideoId))
                        DownloadedVideoState.Instance.RemoveRecord(_projectId, item.MediaVideoId);
                }
                RefreshDownloadAllUI();
                return;
            }

            if (state == ProjectDownloadState.Downloading)
            {
                bool confirmed = false;
                var tcs = new UniTaskCompletionSource();
                ShowConfirmDialog(
                    "Cancel all downloading episodes?",
                    () => { confirmed = true; tcs.TrySetResult(); },
                    () => tcs.TrySetResult()
                );
                await tcs.Task;
                if (!confirmed) return;

                var dm = DownloadManager.Instance;
                if (dm == null) return;

                foreach (var item in playlist)
                {
                    if (string.IsNullOrEmpty(item.MediaVideoId)) continue;
                    dm.Cancel(_projectId, item.MediaVideoId);
                }

                RefreshDownloadAllUI();
                return;
            }

            if (state == ProjectDownloadState.Idle)
            {
                var dm = DownloadManager.Instance;
                if (dm == null) return;

                var reachability = Application.internetReachability;
                if (reachability == NetworkReachability.NotReachable) return;

                if (reachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                {
                    bool confirmed = false;
                    var tcs = new UniTaskCompletionSource();
                    ShowConfirmDialog(
                        "You're on mobile data. Download all episodes?",
                        () => { confirmed = true; tcs.TrySetResult(); },
                        () => tcs.TrySetResult()
                    );
                    await tcs.Task;
                    if (!confirmed) return;
                }

                foreach (var item in playlist)
                {
                    if (string.IsNullOrEmpty(item.MediaVideoId)) continue;
                    if (DownloadedVideoState.Instance.HasDownload(_projectId, item.MediaVideoId)) continue;
                    dm.Enqueue(_projectId, item.MediaVideoId, item.SizeBytes ?? 0);
                }
            }
            // Downloading state: button is non-interactable, this path should not be reached
        }

        // ── Play All ──────────────────────────────────────────────────
        private void OnPlayAll()
        {
            var playlist = _currentDetail?.Playlist;
            if (playlist == null || playlist.Count == 0) return;

            _videoStarted = true;
            int startIndex = FindResumeIndex(playlist);
            PlaybackState.Instance.SetPlaylist(playlist, startIndex);
            UIManager.Instance.GoTo(PageType.PlayVideo,
                new System.ValueTuple<string, PlaylistItem>(_projectId, playlist[startIndex]));
        }

        /// <summary>
        /// Finds the playlist index to resume from using server-returned ProgressMs values.
        /// Returns the first in-progress (not yet complete) episode.
        /// If all watched episodes are complete, returns the one after the last completed episode.
        /// Falls back to index 0 if no progress data exists.
        /// </summary>
        private static int FindResumeIndex(System.Collections.Generic.List<PlaylistItem> playlist)
        {
            int lastCompletedIdx = -1;

            for (int i = 0; i < playlist.Count; i++)
            {
                var item = playlist[i];
                if (item.ProgressMs <= 0) continue;

                long dur = item.DurationMs ?? 0;
                bool isNearlyComplete = dur > 0 && item.ProgressMs >= (long)(dur * 0.95);

                if (!isNearlyComplete) return i;   // in-progress episode — resume here
                lastCompletedIdx = i;
            }

            // All watched episodes are complete → start after the last completed one
            if (lastCompletedIdx >= 0 && lastCompletedIdx + 1 < playlist.Count)
                return lastCompletedIdx + 1;

            return 0;
        }

        // ── Favorite ──────────────────────────────────────────────────
        private void OnFavoriteToggled()
        {
            if (string.IsNullOrEmpty(_projectId)) return;
            ToggleFavoriteAsync().Forget();
        }

        private async UniTaskVoid ToggleFavoriteAsync()
        {
            var local   = UserLocalState.Instance;
            bool wasFav = local.IsFavorite(_projectId);

            // Optimistic update
            if (wasFav) local.RemoveFavorites(new[] { _projectId });
            else        local.AddFavorite(_projectId);
            RefreshFavoriteButton();

            try
            {
                if (wasFav)
                    await ServiceLocator.Instance.FavoriteService.RemoveFavoriteAsync(_projectId);
                else
                    await ServiceLocator.Instance.FavoriteService.AddFavoriteAsync(_projectId);

                AppState.Instance.MarkFavoritesDirty();
            }
            catch (System.Exception e)
            {
                // Revert optimistic update on failure
                Debug.LogError($"[ProjectInfoPage] Favorite toggle failed: {e.Message}");
                if (wasFav) local.AddFavorite(_projectId);
                else        local.RemoveFavorites(new[] { _projectId });
                RefreshFavoriteButton();
            }
        }

        private void RefreshFavoriteButton()
        {
            if (string.IsNullOrEmpty(_projectId)) return;
            var isFav  = UserLocalState.Instance.IsFavorite(_projectId);
            var sprite = isFav ? favoriteActiveSprite : favoriteInactiveSprite;
            if (favoriteIcon != null && sprite != null)
                favoriteIcon.sprite = sprite;
        }

        // ── Overall Progress ──────────────────────────────────────────
        private void RefreshOverallProgress(System.Collections.Generic.List<PlaylistItem> playlist)
        {
            if (overallProgressSlider == null) return;

            long totalDurationMs = 0;
            long totalProgressMs = 0;

            foreach (var item in playlist)
            {
                long dur = item.DurationMs ?? 0;
                totalDurationMs += dur;
                totalProgressMs += System.Math.Min(item.ProgressMs, dur);
            }

            float progress = totalDurationMs > 0
                ? Mathf.Clamp01((float)totalProgressMs / totalDurationMs)
                : 0f;

            overallProgressSlider.interactable = false;
            overallProgressSlider.value        = progress;
            overallProgressSlider.gameObject.SetActive(progress > 0f);
        }

        private void RefreshPlaylistCountText(int count)
        {
            if (playlistCountText == null && playlistContainer != null)
            {
                var countTextTransform = playlistContainer.Find("VideosCountText");
                if (countTextTransform != null)
                    playlistCountText = countTextTransform.GetComponent<TextMeshProUGUI>();
            }

            if (playlistCountText != null)
                playlistCountText.text = $"Playlist  ( {count} videos )";
        }

        private void ClearPlaylistRows()
        {
            if (playlistContainer == null) return;

            for (int i = playlistContainer.childCount - 1; i >= 0; i--)
            {
                var child = playlistContainer.GetChild(i);
                if (child.TryGetComponent<PlaylistItemRow>(out _))
                    Destroy(child.gameObject);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0) return string.Empty;
            var t = System.TimeSpan.FromSeconds(totalSeconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
                : $"{t.Minutes}m {t.Seconds:D2}s";
        }

        private static string ResolveVideoTypeLabel(ProjectDetail detail)
        {
            var playlist = detail.Playlist;
            if (playlist == null || playlist.Count == 0) return "2D";
            var first  = playlist[0];
            var stereo = (first.StereoLayout ?? string.Empty).ToLower();
            var proj   = (first.ProjectionType ?? string.Empty).ToLower();
            bool isSBS = stereo.Contains("sbs") || stereo.Contains("side");
            bool isTB  = stereo.Contains("tb")  || stereo.Contains("top");
            bool is360 = proj.Contains("360");
            bool is180 = proj.Contains("180") || proj.Contains("vr180");
            if (isSBS) return is180 ? "3D 180°"  : is360 ? "3D 360°"  : "3D";
            if (isTB)  return is180 ? "180° TB"  : is360 ? "360° TB"  : "3D TB";
            if (is180) return "180°";
            if (is360) return "360°";
            return "2D";
        }

        private static string FormatTotalSize(ProjectDetail detail)
        {
            if (detail.Playlist == null) return string.Empty;
            long total = 0;
            foreach (var item in detail.Playlist)
                total += item.SizeBytes ?? 0;
            if (total <= 0) return string.Empty;
            if (total >= 1_073_741_824) return $"{total / 1_073_741_824.0:F1} GB";
            if (total >= 1_048_576)     return $"{total / 1_048_576.0:F0} MB";
            return $"{total / 1024.0:F0} KB";
        }

        // ── Confirm Dialog ────────────────────────────────────────────
        private void ShowConfirmDialog(string message, System.Action onYes, System.Action onNo = null)
        {
            if (confirmDialog == null) return;
            if (dialogMessageText != null)
                dialogMessageText.text = message;
            _dialogYesAction = onYes;
            _dialogNoAction  = onNo;
            confirmDialog.SetActive(true);
        }

        private void OnDialogYes()
        {
            confirmDialog.SetActive(false);
            var action = _dialogYesAction;
            _dialogYesAction = null;
            _dialogNoAction  = null;
            action?.Invoke();
        }

        private void OnDialogCancel()
        {
            confirmDialog.SetActive(false);
            var action = _dialogNoAction;
            _dialogYesAction = null;
            _dialogNoAction  = null;
            action?.Invoke();
        }
    }
}
