using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Services;
using Kyalio.State;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Pages
{
    /// <summary>
    /// Project detail page.
    /// param: ProjectNavParam (or a bare string projectId).
    /// Detail comes from GET /api/projects/{projectId}; watch progress is merged from the
    /// local progress cache (populated by /api/me/progress).
    /// </summary>
    public class ProjectInfoPage : MonoBehaviour, IPageHandler, IDevFakeData
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

        private string _projectId;
        private string _entrySource;
        private string _sourceSearchEventId;
        private System.DateTime _startedAt;
        private bool _videoStarted;
        private Project _currentDetail;
        private CancellationTokenSource _cts;

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
        }

        public void OnEnter(object param)
        {
            SubscribeDownloadEvents();

            // Parse param first — both real and fake modes need the projectId.
            string incomingId = null;
            if (param is Kyalio.Models.ProjectNavParam nav)
            {
                incomingId            = nav.ProjectId;
                _entrySource          = nav.Source ?? ProjectPageSource.Direct;
                _sourceSearchEventId  = nav.SearchEventId;
            }
            else if (param is string s)
            {
                incomingId           = s;
                _entrySource         = ProjectPageSource.Direct;
                _sourceSearchEventId = null;
            }

            if (!string.IsNullOrEmpty(incomingId))
            {
                _projectId    = incomingId;
                _startedAt    = System.DateTime.UtcNow;
                _videoStarted = false;
            }
            else if (string.IsNullOrEmpty(_projectId))
            {
                return; // Returning via GoBack with no prior context — nothing to show
            }

            if (DevFlags.UseFakeData)
            {
                LoadFakeData();
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            LoadAsync(_projectId, _cts.Token).Forget();
        }

        public void OnExit()
        {
            UnsubscribeDownloadEvents();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            ReportPageSessionAsync().Forget();
        }

        private async UniTaskVoid ReportPageSessionAsync()
        {
            if (string.IsNullOrEmpty(_projectId)) return;

            var request = new ProjectPageSessionRequest
            {
                ProjectId           = _projectId,
                Source              = _entrySource ?? ProjectPageSource.Direct,
                StartedAt           = _startedAt.ToString("o"),
                DurationMs          = (long)(System.DateTime.UtcNow - _startedAt).TotalMilliseconds,
                VideoStarted        = _videoStarted,
                SourceSearchEventId = _sourceSearchEventId,
            };

            try
            {
                await ServiceLocator.Instance.V2.Analytics
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
            try
            {
                var detail = await ServiceLocator.Instance.V2.Content
                    .GetProjectAsync(projectId, ct);
                if (ct.IsCancellationRequested) return;

                ProjectCacheRepository.Instance.ApplyProjectDetail(detail);
                BindDetail(detail);

                if (thumbnailImage != null && !string.IsNullOrEmpty(detail.ThumbnailUrl))
                {
                    thumbnailImage.sprite = null;
                    thumbnailImage.color  = new Color32(43, 43, 43, 255);
                    var sprite = await ThumbnailLoader.LoadAsync(
                        ThumbnailLoader.Resolve(detail.ThumbnailUrl), ct);
                    if (sprite != null && !ct.IsCancellationRequested)
                    {
                        thumbnailImage.sprite = sprite;
                        thumbnailImage.color  = Color.white;
                    }
                }
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectInfoPage] Load failed: {e.Message}");
            }
        }

        private void BindDetail(Project detail)
        {
            _currentDetail = detail;
            if (detail == null) return;

            var repo = ProjectCacheRepository.Instance;

            if (titleText != null)       titleText.text       = detail.ProjectName ?? string.Empty;
            if (categoryText != null)    categoryText.text    = repo.GetSpecialtyName(detail.SpecialtyId) ?? string.Empty;
            if (contributorText != null) contributorText.text = detail.SurgeonsText ?? string.Empty;
            if (programText != null)     programText.text     = repo.GetFirstProgram(detail)?.Name ?? string.Empty;
            if (descriptionText != null) descriptionText.text = detail.Description ?? string.Empty;

            if (durationText != null)
                durationText.text = FormatDuration(detail.PlaylistDurationSeconds);
            if (videoTypeText != null)
                videoTypeText.text = ResolveVideoTypeLabel(detail);
            if (sizeText != null)
                sizeText.text = FormatTotalSize(detail);

            RefreshFavoriteButton();
            RefreshDownloadAllUI();

            var playlist = detail.Playlist ?? new List<PlaylistItem>();
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
                        new System.ValueTuple<string, PlaylistItem>(_projectId, playlistItem),
                        fade: true);
                };
                row.OnConfirmRequested = (msg, yes, no) => PopupManager.Instance.ShowYesNo(msg, yes, no);
                row.Bind(item, _projectId);
            }
        }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData()
        {
            // The dev seeder populates the V2 cache with full projects (incl. playlists),
            // so just bind the cached project.
            BindDetail(ProjectCacheRepository.Instance.Get(_projectId));
        }

        // ── Download All — State Machine ──────────────────────────────
        private enum ProjectDownloadState { Idle, Downloading, AllDownloaded }

        private ProjectDownloadState GetProjectDownloadState()
        {
            var playlist = _currentDetail?.Playlist;
            if (playlist == null || playlist.Count == 0) return ProjectDownloadState.Idle;

            bool allDownloaded = true;
            foreach (var item in playlist)
            {
                if (string.IsNullOrEmpty(item.VideoId)) continue;
                if (!DownloadedVideoState.Instance.HasDownload(_projectId, item.VideoId))
                { allDownloaded = false; break; }
            }
            if (allDownloaded) return ProjectDownloadState.AllDownloaded;

            var dm = DownloadManager.Instance;
            if (dm != null)
            {
                bool allCommitted = true;
                foreach (var item in playlist)
                {
                    if (string.IsNullOrEmpty(item.VideoId)) continue;
                    if (!DownloadedVideoState.Instance.HasDownload(_projectId, item.VideoId) &&
                        !dm.IsActive(_projectId, item.VideoId))
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

            downloadAllButton.interactable = true;

            if (downloadAllStatusImage != null)
                downloadAllStatusImage.gameObject.SetActive(!isDownloading);

            if (downloadAllProgressSlider != null)
                downloadAllProgressSlider.gameObject.SetActive(isDownloading);

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
                if (string.IsNullOrEmpty(item.VideoId)) continue;
                long size = item.SizeBytes;
                totalSize += size;

                if (DownloadedVideoState.Instance.HasDownload(_projectId, item.VideoId))
                    downloadedSize += size;
                else if (item.VideoId == currentVideoId)
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
                PopupManager.Instance.ShowYesNo(
                    "Delete all downloaded episodes?",
                    () => { confirmed = true; tcs.TrySetResult(); },
                    () => tcs.TrySetResult()
                );
                await tcs.Task;
                if (!confirmed) return;

                var dm = DownloadManager.Instance;
                foreach (var item in playlist)
                {
                    if (!string.IsNullOrEmpty(item.VideoId))
                        dm?.Delete(_projectId, item.VideoId);
                }
                RefreshDownloadAllUI();
                return;
            }

            if (state == ProjectDownloadState.Downloading)
            {
                bool confirmed = false;
                var tcs = new UniTaskCompletionSource();
                PopupManager.Instance.ShowYesNo(
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
                    if (string.IsNullOrEmpty(item.VideoId)) continue;
                    dm.Cancel(_projectId, item.VideoId);
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
                    PopupManager.Instance.ShowYesNo(
                        "You're on mobile data. Download all episodes?",
                        () => { confirmed = true; tcs.TrySetResult(); },
                        () => tcs.TrySetResult()
                    );
                    await tcs.Task;
                    if (!confirmed) return;
                }

                foreach (var item in playlist)
                {
                    if (string.IsNullOrEmpty(item.VideoId)) continue;
                    if (DownloadedVideoState.Instance.HasDownload(_projectId, item.VideoId)) continue;
                    dm.Enqueue(_projectId, item.VideoId, item.SizeBytes);
                }
            }
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
                new System.ValueTuple<string, PlaylistItem>(_projectId, playlist[startIndex]),
                fade: true);
        }

        /// <summary>
        /// Finds the playlist index to resume from using cached per-video progress.
        /// Returns the first in-progress (not yet complete) episode; if all watched
        /// episodes are complete, the one after the last completed; else index 0.
        /// </summary>
        private static int FindResumeIndex(List<PlaylistItem> playlist)
        {
            var repo = ProjectCacheRepository.Instance;
            int lastCompletedIdx = -1;

            for (int i = 0; i < playlist.Count; i++)
            {
                var item = playlist[i];
                int progressMs = repo.GetProgressMs(item.VideoId);
                if (progressMs <= 0) continue;

                long dur = item.DurationMs;
                bool isNearlyComplete = dur > 0 && progressMs >= (long)(dur * 0.95);

                if (!isNearlyComplete) return i;   // in-progress episode — resume here
                lastCompletedIdx = i;
            }

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
                    await ServiceLocator.Instance.V2.Favorites.RemoveFavoriteAsync(_projectId);
                else
                    await ServiceLocator.Instance.V2.Favorites.AddFavoriteAsync(_projectId);

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
        private void RefreshOverallProgress(List<PlaylistItem> playlist)
        {
            if (overallProgressSlider == null) return;

            var repo = ProjectCacheRepository.Instance;
            long totalDurationMs = 0;
            long totalProgressMs = 0;

            foreach (var item in playlist)
            {
                long dur = item.DurationMs;
                totalDurationMs += dur;
                totalProgressMs += System.Math.Min(repo.GetProgressMs(item.VideoId), dur);
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

        private static string ResolveVideoTypeLabel(Project detail)
        {
            var playlist = detail.Playlist;
            if (playlist == null || playlist.Count == 0) return "2D";
            return playlist[0].PlaybackMode switch
            {
                PlaybackMode.Vr180Sbs  => "3D 180°",
                PlaybackMode.Vr360Mono => "360°",
                _                      => "2D",
            };
        }

        private static string FormatTotalSize(Project detail)
        {
            long total = detail.TotalSizeBytes;
            if (total <= 0 && detail.Playlist != null)
                foreach (var item in detail.Playlist)
                    total += item.SizeBytes;

            if (total <= 0) return string.Empty;
            if (total >= 1_073_741_824) return $"{total / 1_073_741_824.0:F1} GB";
            if (total >= 1_048_576)     return $"{total / 1_048_576.0:F0} MB";
            return $"{total / 1024.0:F0} KB";
        }
    }
}
