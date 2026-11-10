using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Selectable list item shared by MyFavoritesPage and MyDownloadsPage.
    ///
    /// UI layout (prefab):
    ///   [toggleContainer]  — shown only in edit mode; contains the Toggle (checkbox)
    ///   [thumbnail]        — project thumbnail image
    ///   [programPicImage]  — program logo (hidden when empty)
    ///   [titleText]        — project name
    ///   [categoryText]     — category name
    ///   [drNameText]       — "By Dr. {name}"
    ///   [videoCountText]   — number of videos (e.g. "3")
    ///   [sizeText]         — formatted size (project total / downloaded bytes)
    ///   [isListSign]       — playlist icon; shown when videoCount > 1
    ///   [button]           — full-row tap to navigate (active in both modes)
    ///
    /// Inspector: thumbnail, programPicImage, titleText, categoryText, drNameText,
    ///            videoCountText, isListSign, toggleContainer, toggle, button
    /// </summary>
    public class SelectableListItem : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private Image programPicImage;         // nullable
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;  // nullable
        [SerializeField] private TextMeshProUGUI drNameText;    // nullable, shown as "By Dr. …"
        [SerializeField] private TextMeshProUGUI videoCountText;// nullable
        [SerializeField] private TextMeshProUGUI sizeText;      // nullable; hidden when no size
        [SerializeField] private GameObject isListSign;         // nullable; visible when videoCount > 1
        [SerializeField] private GameObject toggleContainer;    // parent of toggle; hidden in normal mode
        [SerializeField] private Toggle toggle;
        [SerializeField] private Button button;

        private string _projectId;
        private CancellationTokenSource _cts;

        public System.Action<string, bool> OnSelectionChanged;
        public System.Action<string> OnItemClicked;

        private void Awake()
        {
            ThumbnailLoader.Track(thumbnail, programPicImage);
            toggle.onValueChanged.AddListener(isOn =>
                OnSelectionChanged?.Invoke(_projectId, isOn));

            button.onClick.AddListener(() =>
                OnItemClicked?.Invoke(_projectId));
        }

        /// <summary>
        /// Binds all display data to this item.
        /// </summary>
        public void Bind(string projectId, string title, string category, string drName,
            string thumbnailUrl, string programPicUrl, int videoCount, string sizeLabel,
            bool prefixWithDr = true, bool isSelected = false)
        {
            _projectId = projectId;

            titleText.text = title ?? string.Empty;

            if (categoryText != null)
                categoryText.text = category ?? string.Empty;

            if (drNameText != null)
            {
                if (string.IsNullOrEmpty(drName)) drNameText.text = string.Empty;
                else drNameText.text = prefixWithDr ? $"By Dr. {drName}" : drName;
            }

            if (videoCountText != null)
                videoCountText.text = videoCount > 0 ? videoCount.ToString() : string.Empty;

            if (isListSign != null)
                isListSign.SetActive(videoCount > 1);

            if (sizeText != null)
            {
                sizeText.gameObject.SetActive(!string.IsNullOrEmpty(sizeLabel));
                sizeText.text = sizeLabel ?? string.Empty;
            }

            toggle.SetIsOnWithoutNotify(isSelected);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            ImageReveal.Reset(thumbnail);
            if (!string.IsNullOrEmpty(thumbnailUrl))
                LoadImageAsync(thumbnailUrl, thumbnail, _cts.Token, asThumbnail: true).Forget();

            if (programPicImage != null)
            {
                programPicImage.gameObject.SetActive(!string.IsNullOrEmpty(programPicUrl));
                ImageReveal.HideUntilLoaded(programPicImage);
                if (!string.IsNullOrEmpty(programPicUrl))
                    LoadImageAsync(programPicUrl, programPicImage, _cts.Token, asThumbnail: false).Forget();
            }
        }

        /// <summary>
        /// Shows or hides the toggle container for edit mode.
        /// Deselects the toggle when leaving edit mode.
        /// </summary>
        public void SetEditMode(bool isEdit)
        {
            if (toggleContainer != null)
                toggleContainer.SetActive(isEdit);

            if (!isEdit)
                toggle.SetIsOnWithoutNotify(false);
        }

        public void SetSelected(bool isSelected)
        {
            toggle.SetIsOnWithoutNotify(isSelected);
        }

        public bool IsSelected => toggle.isOn;
        public string ProjectId => _projectId;

        // asThumbnail: brighten from the dark placeholder; otherwise (program logo) fade in.
        private async UniTaskVoid LoadImageAsync(string url, Image target, CancellationToken ct,
            bool asThumbnail)
        {
            int startFrame = Time.frameCount;
            var sprite = await ThumbnailLoader.LoadAsync(ThumbnailLoader.Resolve(url), ct);
            if (sprite == null || ct.IsCancellationRequested) return;

            if (asThumbnail) ImageReveal.ShowThumbnail(target, sprite, startFrame);
            else             ImageReveal.ShowFadeIn(target, sprite, startFrame);
        }

        /// <summary>Cancels in-flight image loads; call before returning the item to a pool.</summary>
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
