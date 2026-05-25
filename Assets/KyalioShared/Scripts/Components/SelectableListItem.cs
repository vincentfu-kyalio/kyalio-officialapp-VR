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
            toggle.onValueChanged.AddListener(isOn =>
                OnSelectionChanged?.Invoke(_projectId, isOn));

            button.onClick.AddListener(() =>
                OnItemClicked?.Invoke(_projectId));
        }

        /// <summary>
        /// Binds all display data to this item.
        /// </summary>
        public void Bind(string projectId, string title, string category, string drName,
            string thumbnailUrl, string programPicUrl, int videoCount,
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

            toggle.SetIsOnWithoutNotify(isSelected);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            thumbnail.sprite = null;
            thumbnail.color  = new Color32(43, 43, 43, 255);
            if (!string.IsNullOrEmpty(thumbnailUrl))
                LoadImageAsync(thumbnailUrl, thumbnail, _cts.Token).Forget();

            if (programPicImage != null)
            {
                programPicImage.gameObject.SetActive(!string.IsNullOrEmpty(programPicUrl));
                if (!string.IsNullOrEmpty(programPicUrl))
                    LoadImageAsync(programPicUrl, programPicImage, _cts.Token).Forget();
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

        private async UniTaskVoid LoadImageAsync(string url, Image target, CancellationToken ct)
        {
            var sprite = await ThumbnailLoader.LoadAsync(ThumbnailLoader.Resolve(url), ct);
            if (sprite != null && !ct.IsCancellationRequested)
            {
                target.sprite = sprite;
                target.color  = Color.white;
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }
    }
}
