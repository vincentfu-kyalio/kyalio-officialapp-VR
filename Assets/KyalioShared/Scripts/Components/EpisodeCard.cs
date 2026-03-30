using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Episode card for the Series page (episodes mode).
    /// Displays a single RoleContentEpisode with thumbnail, title, duration, and a progress bar placeholder.
    /// Inspector: thumbnail, titleText, durationText, progressBar, button
    /// </summary>
    public class EpisodeCard : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Button button;

        private RoleContentEpisode _episode;
        private CancellationTokenSource _cts;

        public System.Action<RoleContentEpisode> OnClicked;

        private void Awake()
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(_episode));

            if (progressBar != null)
                progressBar.interactable = false;
        }

        public void Bind(RoleContentEpisode episode)
        {
            _episode = episode;

            titleText.text = episode.Title ?? string.Empty;

            if (durationText != null)
                durationText.text = FormatDuration(episode.DurationMs);

            if (progressBar != null)
            {
                bool hasProgress = episode.ProgressMs > 0;
                progressBar.gameObject.SetActive(hasProgress);
                progressBar.value = (hasProgress && episode.DurationMs.HasValue && episode.DurationMs.Value > 0)
                    ? Mathf.Clamp01((float)episode.ProgressMs / episode.DurationMs.Value)
                    : 0f;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            bool hasThumbnail = !string.IsNullOrEmpty(episode.ThumbnailUrl);
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail)
                LoadThumbnailAsync(episode.ThumbnailUrl, _cts.Token).Forget();
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

        private static string FormatDuration(int? durationMs)
        {
            if (durationMs == null || durationMs <= 0) return string.Empty;
            var t = System.TimeSpan.FromMilliseconds(durationMs.Value);
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
