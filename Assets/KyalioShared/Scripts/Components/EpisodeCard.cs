using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Episode card for the Series page (episodes mode).
    /// Displays a single playlist video plus its watch-progress bar.
    /// Inspector: thumbnail, titleText, durationText, progressBar, button
    /// </summary>
    public class EpisodeCard : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Button button;

        private string _projectId;
        private PlaylistItem _item;
        private CancellationTokenSource _cts;

        /// <summary>(projectId, video) of the clicked episode.</summary>
        public System.Action<string, PlaylistItem> OnClicked;

        private void Awake()
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(_projectId, _item));

            if (progressBar != null)
                progressBar.interactable = false;
        }

        public void Bind(string projectId, PlaylistItem item, int progressMs)
        {
            _projectId = projectId;
            _item      = item;

            titleText.text = item.Title ?? string.Empty;

            if (durationText != null)
                durationText.text = FormatDuration(item.DurationMs);

            if (progressBar != null)
            {
                bool hasProgress = progressMs > 0 && item.DurationMs > 0;
                progressBar.gameObject.SetActive(hasProgress);
                progressBar.value = hasProgress
                    ? Mathf.Clamp01((float)progressMs / item.DurationMs)
                    : 0f;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            bool hasThumbnail = !string.IsNullOrEmpty(item.ThumbnailUrl);
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail)
                LoadThumbnailAsync(ThumbnailLoader.Resolve(item.ThumbnailUrl), _cts.Token).Forget();
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

        private static string FormatDuration(int durationMs)
        {
            if (durationMs <= 0) return string.Empty;
            var t = System.TimeSpan.FromMilliseconds(durationMs);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
                : $"{t.Minutes}m {t.Seconds:D2}s";
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }
    }
}
