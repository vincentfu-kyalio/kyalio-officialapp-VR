using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using Kyalio.Repositories;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Recently Watched card (RW_block.prefab).
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
            button.onClick.AddListener(() => OnClicked?.Invoke(_projectId, _videoId));
        }

        public void Bind(WatchHistoryProjectItem item)
        {
            _projectId = item.ProjectId;
            _videoId   = item.LatestEpisode.MediaVideoId;

            titleText.text = item.ProjectName;
            if (categoryText != null)
                categoryText.text = item.CategoryName ?? string.Empty;

            if (episodeText != null)
                episodeText.text = (item.LatestEpisode.Ordinal + 1).ToString(); // Ordinal is 0-based

            if (progressBar != null)
                progressBar.value = item.LatestEpisode.DurationMs > 0
                    ? Mathf.Clamp01((float)item.LatestEpisode.ProgressMs / item.LatestEpisode.DurationMs)
                    : 0f;

            int videoCount = ProjectCacheRepository.Instance.GetVideoCount(item.ProjectId);
            if (playlistSign != null)
                playlistSign.SetActive(videoCount > 1);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            LoadThumbnailAsync(item.ThumbnailUrl, _cts.Token).Forget();

            if (programLogo != null)
            {
                bool hasProgramLogo = !string.IsNullOrEmpty(item.ProgramPicUrl);
                programLogo.sprite = null;
                programLogo.gameObject.SetActive(hasProgramLogo);
                if (hasProgramLogo)
                    LoadProgramLogoAsync(item.ProgramPicUrl, _cts.Token).Forget();
            }
        }

        public void CancelLoads()
        {
            _cts?.Cancel();
            _cts = null;
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

        private async UniTaskVoid LoadProgramLogoAsync(string url, CancellationToken ct)
        {
            var sprite = await ThumbnailLoader.LoadAsync(url, ct);
            if (sprite != null && !ct.IsCancellationRequested)
                programLogo.sprite = sprite;
        }

        private void OnDestroy() => _cts?.Cancel();
    }
}
