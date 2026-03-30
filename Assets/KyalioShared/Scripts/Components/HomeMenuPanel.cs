using System.Collections.Generic;
using Kyalio.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    public class HomeMenuSelection
    {
        public enum Kind { LatestReleases, Recommended, Category }

        public Kind SelectionKind;
        public string CategoryId;
        public string CategoryName;
    }

    /// <summary>
    /// HomePage menu: fixed Explorer items (Latest Releases, Recommended) + dynamic Categories.
    /// panelToggle.isOn controls the show/hide of menuBody.
    /// Inspector:
    ///   panelToggle       — Always-visible Toggle that opens/closes the entire menu
    ///   menuBody          — Main menu body GameObject (controlled by panelToggle)
    ///   togglePrefab      — Menu item Toggle (with TMP child)
    ///   explorerContainer — Parent for fixed items
    ///   categoryContainer — Parent for dynamic Category items
    ///   toggleGroup       — ToggleGroup (ensures single selection)
    /// </summary>
    public class HomeMenuPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private Toggle panelToggle;
        [SerializeField] private GameObject menuBody;

        [Header("Menu Items")]
        [SerializeField] private Toggle togglePrefab;
        [SerializeField] private Transform explorerContainer;
        [SerializeField] private Transform categoryContainer;
        [SerializeField] private ToggleGroup toggleGroup;

        public event System.Action<HomeMenuSelection> OnSelectionChanged;

        public HomeMenuSelection CurrentSelection { get; private set; }

        private readonly List<Toggle> _toggles = new();

        private void Awake()
        {
            CurrentSelection = new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases };

            if (panelToggle != null)
                panelToggle.onValueChanged.AddListener(OnPanelToggleChanged);

            BuildExplorer();
        }

        // ── Public API ────────────────────────────────────────────────

        public void BuildCategories(List<Category> categories)
        {
            foreach (Transform child in categoryContainer)
                Destroy(child.gameObject);

            if (categories == null) return;

            foreach (var cat in categories)
            {
                CreateToggle(categoryContainer, cat.Name, new HomeMenuSelection
                {
                    SelectionKind = HomeMenuSelection.Kind.Category,
                    CategoryId    = cat.Id,
                    CategoryName  = cat.Name
                });
            }
        }

        // ── Private ───────────────────────────────────────────────────

        private void OnPanelToggleChanged(bool isOn)
        {
            if (menuBody != null)
                menuBody.SetActive(isOn);
        }

        private void BuildExplorer()
        {
            var latestToggle = CreateToggle(explorerContainer, "Latest Releases",
                new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases });
            CreateToggle(explorerContainer, "Recommended",
                new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.Recommended });

            // Pre-select Latest Releases without firing event (subscribers not yet attached)
            latestToggle.SetIsOnWithoutNotify(true);
        }

        private Toggle CreateToggle(Transform parent, string label, HomeMenuSelection selection)
        {
            var toggle = Instantiate(togglePrefab, parent);
            toggle.group = toggleGroup;

            var text = toggle.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;

            toggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                CurrentSelection = selection;
                OnSelectionChanged?.Invoke(selection);
            });

            _toggles.Add(toggle);
            return toggle;
        }
    }
}
