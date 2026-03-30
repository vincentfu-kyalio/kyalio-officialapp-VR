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
    /// A single Project card.
    /// Attach to the ProjectCard prefab.
    /// Inspector: thumbnail, title, drName, duration (optional)
    /// </summary>
    public class ProjectCard : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI drNameText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private GameObject playlistSign;
        [SerializeField] private Image programLogo;
        [SerializeField] private Button button;

        private SubscribedProject _project;
        private CancellationTokenSource _cts;

        // Click callback, set by ProjectCardList
        public System.Action<SubscribedProject> OnClicked;

        private void Awake()
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(_project));
        }

        /// <summary>
        /// Populates the card with data and loads the thumbnail.
        /// </summary>
        public void Bind(SubscribedProject project)
        {
            _project = project;

            // Title
            titleText.text = project.Name;

            // Dr Name
            if (drNameText != null)
                drNameText.text = project.DrName ?? string.Empty;

            // Category
            if (categoryText != null)
                categoryText.text = project.CategoryName ?? string.Empty;

            // Playlist count — now directly from API
            int videoCount = project.PlaylistCount > 0
                ? project.PlaylistCount
                : ProjectCacheRepository.Instance.GetVideoCount(project.Id);
            if (numberText != null)
                numberText.text = videoCount > 0 ? videoCount.ToString() : "";
            if (playlistSign != null)
                playlistSign.SetActive(videoCount > 1);

            // Duration
            int totalSec = project.PlaylistDurationSeconds > 0
                ? project.PlaylistDurationSeconds
                : ProjectCacheRepository.Instance.GetPlaylistDurationSeconds(project.Id);
            if (durationText != null)
                durationText.text = FormatDuration(totalSec);

            // Cancel previous loads
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Thumbnail
            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            bool hasThumbnail = !string.IsNullOrEmpty(project.ThumbnailUrl);
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail)
                LoadThumbnailAsync(project.ThumbnailUrl, _cts.Token).Forget();

            // Program logo
            if (programLogo != null)
            {
                programLogo.gameObject.SetActive(!string.IsNullOrEmpty(project.ProgramPicUrl));
                if (!string.IsNullOrEmpty(project.ProgramPicUrl))
                    LoadProgramLogoAsync(project.ProgramPicUrl, _cts.Token).Forget();
            }
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

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0) return "";
            var t = System.TimeSpan.FromSeconds(totalSeconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
                : $"{t.Minutes}m {t.Seconds:D2}s";
        }

        private async UniTaskVoid LoadProgramLogoAsync(string url, CancellationToken ct)
        {
            var sprite = await ThumbnailLoader.LoadAsync(url, ct);
            if (sprite != null && !ct.IsCancellationRequested)
                programLogo.sprite = sprite;
        }

        /// <summary>
        /// Cancels any in-flight thumbnail/logo requests without destroying the object.
        /// Call this before returning the card to a pool, or when the parent page exits.
        /// </summary>
        public void CancelLoads()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }
    }
}
