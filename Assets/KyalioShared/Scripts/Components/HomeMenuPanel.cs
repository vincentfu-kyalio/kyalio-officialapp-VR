using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    public class HomeMenuSelection
    {
        public enum Kind { LatestReleases, Recommended, Category }

        public Kind SelectionKind;

        /// <summary>Specialty id (the "Category" kind now maps to a specialty filter).</summary>
        public string CategoryId;
        public string CategoryName;
    }

    /// <summary>
    /// HomePage left sidebar: fixed items (Latest Releases, Recommended) + dynamic Categories.
    /// Each item is a Button; HomeMenuPanel tracks the selected item and toggles a
    /// "SelectedIndicator" child GameObject on the prefab for visual feedback.
    /// Inspector: menuItemPrefab, explorerContainer, categoryContainer
    /// </summary>
    public class HomeMenuPanel : MonoBehaviour
    {
        [Header("Menu Items")]
        [SerializeField] private Button menuItemPrefab;
        [SerializeField] private Transform explorerContainer;
        [SerializeField] private Transform categoryContainer;

        public event System.Action<HomeMenuSelection> OnSelectionChanged;

        public HomeMenuSelection CurrentSelection { get; private set; }

        private readonly List<(Button button, HomeMenuSelection selection)> _items = new();
        private Button _selectedButton;

        private void Awake()
        {
            CurrentSelection = new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases };
            BuildExplorer();
        }

        // ── Public API ────────────────────────────────────────────────

        public void BuildCategories(List<IdNameRef> categories)
        {
            // Remove previously built category buttons
            foreach (Transform child in categoryContainer)
                Destroy(child.gameObject);

            _items.RemoveAll(item => item.button == null);

            if (categories == null) return;

            foreach (var cat in categories)
            {
                CreateItem(categoryContainer, cat.Name, new HomeMenuSelection
                {
                    SelectionKind = HomeMenuSelection.Kind.Category,
                    CategoryId    = cat.Id,
                    CategoryName  = cat.Name
                });
            }
        }

        // ── Private ───────────────────────────────────────────────────

        private void BuildExplorer()
        {
            var latestButton = CreateItem(explorerContainer, "Latest Releases",
                new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases });

            CreateItem(explorerContainer, "Recommended",
                new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.Recommended });

            // Pre-select Latest Releases without firing event (subscribers not yet attached)
            SetSelectedVisual(latestButton, true);
            _selectedButton = latestButton;
        }

        private Button CreateItem(Transform parent, string label, HomeMenuSelection selection)
        {
            var button = Instantiate(menuItemPrefab, parent);

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;

            SetSelectedVisual(button, false);
            button.onClick.AddListener(() => SelectItem(button, selection));

            _items.Add((button, selection));
            return button;
        }

        private void SelectItem(Button button, HomeMenuSelection selection)
        {
            if (_selectedButton != null)
                SetSelectedVisual(_selectedButton, false);

            _selectedButton = button;
            SetSelectedVisual(button, true);

            CurrentSelection = selection;
            OnSelectionChanged?.Invoke(selection);
        }

        /// <summary>
        /// Looks for a child named "SelectedIndicator" on the button prefab and
        /// shows or hides it. If the child doesn't exist, nothing happens.
        /// </summary>
        private static void SetSelectedVisual(Button button, bool selected)
        {
            var indicator = button.transform.Find("SelectedIndicator");
            if (indicator != null)
                indicator.gameObject.SetActive(selected);
        }
    }
}
