using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Recently Watched card (RW_block.prefab).
    /// Binds a V2 WatchHistoryItem and hydrates display metadata from the local Project cache.
    /// Inspector: thumbnail, programLogo, titleText, categoryText, episodeText, progressBar, playlistSign, button
    /// </summary>
    public class RecentlyWatchedCard : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private Image programLogo;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI episodeText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private GameObject playlistSign;
        [SerializeField] private Button button;

        private string _projectId;
        private string _videoId;
        private CancellationTokenSource _cts;

        public System.Action<string, string> OnClicked; // projectId, videoId

        private void Awake()
        {
            ThumbnailLoader.Track(thumbnail, programLogo);
            SliderFillClip.Install(progressBar);
            button.onClick.AddListener(() => OnClicked?.Invoke(_projectId, _videoId));
        }

        public void Bind(WatchHistoryItem item)
        {
            _projectId = item.ProjectId;
            _videoId   = item.VideoId;

            var repo    = ProjectCacheRepository.Instance;
            var project = repo.Get(item.ProjectId);
            var video   = repo.GetVideo(item.ProjectId, item.VideoId);

            titleText.text = project?.ProjectName ?? string.Empty;
            if (categoryText != null)
                categoryText.text = repo.GetSpecialtyName(project?.SpecialtyId) ?? string.Empty;

            if (episodeText != null)
            {
                int idx = repo.GetVideoIndex(item.ProjectId, item.VideoId);
                episodeText.text = idx >= 0 ? $"<size=90%>ep</size>{idx + 1}" : string.Empty;
            }

            if (progressBar != null)
            {
                int durationMs = video?.DurationMs ?? 0;
                progressBar.value = durationMs > 0
                    ? Mathf.Clamp01((float)item.ProgressMs / durationMs)
                    : 0f;
            }

            int videoCount = project?.PlaylistCount ?? 0;
            if (playlistSign != null)
                playlistSign.SetActive(videoCount > 1);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            ImageReveal.Reset(thumbnail);
            var thumbUrl = ThumbnailUrlFor(item);
            if (!string.IsNullOrEmpty(thumbUrl))
                LoadThumbnailAsync(thumbUrl, _cts.Token).Forget();

            if (programLogo != null)
            {
                var picUrl = ProgramLogoUrlFor(item);
                programLogo.sprite = null;
                ImageReveal.HideUntilLoaded(programLogo);
                programLogo.gameObject.SetActive(!string.IsNullOrEmpty(picUrl));
                if (!string.IsNullOrEmpty(picUrl))
                    LoadProgramLogoAsync(picUrl, _cts.Token).Forget();
            }
        }

        /// <summary>
        /// Resolved thumbnail URL a card shows for this item: the watched episode's own
        /// thumbnail, falling back to the project's. Null when there is none.
        /// </summary>
        public static string ThumbnailUrlFor(WatchHistoryItem item)
        {
            var repo = ProjectCacheRepository.Instance;
            var url  = repo.GetVideo(item.ProjectId, item.VideoId)?.ThumbnailUrl
                       ?? repo.Get(item.ProjectId)?.ThumbnailUrl;
            return string.IsNullOrEmpty(url) ? null : ThumbnailLoader.Resolve(url);
        }

        /// <summary>Resolved program logo URL a card shows for this item, or null.</summary>
        public static string ProgramLogoUrlFor(WatchHistoryItem item)
        {
            var repo = ProjectCacheRepository.Instance;
            var url  = repo.GetFirstProgram(repo.Get(item.ProjectId))?.PicUrl;
            return string.IsNullOrEmpty(url) ? null : ThumbnailLoader.Resolve(url);
        }

        public void CancelLoads()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async UniTaskVoid LoadThumbnailAsync(string url, CancellationToken ct)
        {
            int startFrame = Time.frameCount;
            var sprite = await ThumbnailLoader.LoadAsync(url, ct);
            if (sprite != null && !ct.IsCancellationRequested)
                ImageReveal.ShowThumbnail(thumbnail, sprite, startFrame);
        }

        private async UniTaskVoid LoadProgramLogoAsync(string url, CancellationToken ct)
        {
            int startFrame = Time.frameCount;
            var sprite = await ThumbnailLoader.LoadAsync(url, ct);
            if (sprite != null && !ct.IsCancellationRequested)
                ImageReveal.ShowFadeIn(programLogo, sprite, startFrame);
        }

        private void OnDestroy() => _cts?.Cancel();
    }
}
