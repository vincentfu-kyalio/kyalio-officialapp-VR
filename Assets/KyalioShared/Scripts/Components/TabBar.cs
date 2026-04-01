using Kyalio.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Persistent tab bar with five entries: Home, Search, Series, MyKyalio, Exit App.
    /// Home / Search / Series / MyKyalio share a ToggleGroup.
    /// Exit App shows a confirmation popup and never stays selected.
    ///
    /// Inspector:
    ///   homeToggle, searchToggle, seriesToggle, myKyalioToggle — inside a ToggleGroup
    ///   exitAppToggle — standalone, not in the group
    /// </summary>
    public class TabBar : MonoBehaviour
    {
        public static TabBar Instance { get; private set; }

        [Header("Navigation toggles (ToggleGroup)")]
        [SerializeField] private Toggle homeToggle;
        [SerializeField] private Toggle searchToggle;
        [SerializeField] private Toggle seriesToggle;
        [SerializeField] private Toggle myKyalioToggle;

        [Header("Exit App (standalone)")]
        [SerializeField] private Toggle exitAppToggle;

        // Prevent navigation callbacks when updating toggle visuals programmatically
        private bool _suppressNavigation;
        private Toggle _activeToggle;

        private void Awake()
        {
            Instance = this;

            homeToggle.onValueChanged.AddListener(on      => { if (on) OnNavToggle(homeToggle,      PageType.Home); });
            searchToggle.onValueChanged.AddListener(on    => { if (on) OnNavToggle(searchToggle,    PageType.Search); });
            seriesToggle.onValueChanged.AddListener(on    => { if (on) OnNavToggle(seriesToggle,    PageType.Series); });
            myKyalioToggle.onValueChanged.AddListener(on  => { if (on) OnNavToggle(myKyalioToggle,  PageType.MyKyalio); });
            exitAppToggle.onValueChanged.AddListener(on   => { if (on) OnExitAppToggled(); });
        }

        /// <summary>
        /// Update the tab bar selection to match the given page without triggering navigation.
        /// Call this when navigating to a top-level page from code (e.g. after login).
        /// </summary>
        public void SelectTab(PageType pageType)
        {
            var target = ToggleForPage(pageType);
            if (target == null || target == _activeToggle) return;

            _suppressNavigation = true;
            target.SetIsOnWithoutNotify(true);
            if (_activeToggle != null) _activeToggle.SetIsOnWithoutNotify(false);
            _activeToggle = target;
            _suppressNavigation = false;
        }

        // ── Private ───────────────────────────────────────────────────

        private void OnNavToggle(Toggle toggle, PageType pageType)
        {
            _activeToggle = toggle;
            if (_suppressNavigation) return;
            UIManager.Instance.SwitchPage(pageType);
        }

        private void OnExitAppToggled()
        {
            // Restore previous tab immediately — Exit App never stays selected
            _suppressNavigation = true;
            exitAppToggle.SetIsOnWithoutNotify(false);
            if (_activeToggle != null) _activeToggle.SetIsOnWithoutNotify(true);
            _suppressNavigation = false;

            PopupManager.Instance.ShowYesNo(
                "Exit application?",
                onYes: () => Application.Quit()
            );
        }

        private Toggle ToggleForPage(PageType pageType) => pageType switch
        {
            PageType.Home      => homeToggle,
            PageType.Search    => searchToggle,
            PageType.Series    => seriesToggle,
            PageType.MyKyalio  => myKyalioToggle,
            _                  => null
        };
    }
}
