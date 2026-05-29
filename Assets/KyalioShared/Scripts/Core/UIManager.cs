using System.Collections.Generic;
using UnityEngine;

namespace Kyalio.Core
{
    /// <summary>
    /// Central navigation manager. Owns a stack-based page history.
    ///
    /// Usage:
    ///   UIManager.Instance.GoTo(PageType.ProjectInfo, new ProjectNavParam { ... });
    ///   UIManager.Instance.GoBack();
    ///
    /// Inspector: assign each PageType → IPageHandler MonoBehaviour in the Pages array.
    /// All registered page GameObjects are hidden on Awake; UIManager controls visibility.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private PageEntry[] pages;

        [System.Serializable]
        private class PageEntry
        {
            public PageType pageType;
            public GameObject pageObject;
        }

        private readonly Dictionary<PageType, IPageHandler> _pageMap = new();
        private readonly Stack<PageType> _history = new();
        private PageType? _currentPage;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var entry in pages)
            {
                if (entry.pageObject == null)
                {
                    Debug.LogWarning($"[UIManager] pageObject is null for {entry.pageType}.");
                    continue;
                }

                var handler = entry.pageObject.GetComponent<IPageHandler>();
                if (handler != null)
                {
                    _pageMap[entry.pageType] = handler;
                    entry.pageObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"[UIManager] {entry.pageObject.name} has no IPageHandler component for {entry.pageType}.", entry.pageObject);
                }
            }
        }

        /// <summary>
        /// Switch to a top-level page (tab navigation).
        /// Clears the history stack so GoBack cannot leave the new page.
        /// Use this from TabBar; use GoTo for deep navigation within a tab.
        /// </summary>
        public void SwitchPage(PageType pageType)
        {
            if (_currentPage.HasValue)
                ExitPage(_currentPage.Value);

            _history.Clear();
            EnterPage(pageType, null);
            _currentPage = pageType;
        }

        /// <summary>
        /// Navigate to a page, pushing the current page onto the history stack.
        /// Pass null for param when no data needs to be forwarded.
        /// When fade is true (and a SceneFader exists), the page swap is hidden behind a
        /// fade out / fade in so the old and new pages never flicker against each other.
        /// </summary>
        public void GoTo(PageType pageType, object param = null, bool fade = false)
        {
            if (fade && SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOutThenIn(() => GoToImmediate(pageType, param));
                return;
            }
            GoToImmediate(pageType, param);
        }

        private void GoToImmediate(PageType pageType, object param)
        {
            if (_currentPage.HasValue)
            {
                ExitPage(_currentPage.Value);
                _history.Push(_currentPage.Value);
            }

            EnterPage(pageType, param);
            _currentPage = pageType;
        }

        /// <summary>
        /// Return to the previous page. The previous page's OnEnter is called with null
        /// so pages can distinguish a GoBack re-entry from a fresh navigation.
        /// Does nothing if there is no history.
        /// When fade is true (and a SceneFader exists), the page swap is hidden behind a
        /// fade out / fade in.
        /// </summary>
        public void GoBack(bool fade = false)
        {
            if (_history.Count == 0)
            {
                Debug.LogWarning("[UIManager] GoBack called with empty history.");
                return;
            }

            if (fade && SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOutThenIn(GoBackImmediate);
                return;
            }
            GoBackImmediate();
        }

        private void GoBackImmediate()
        {
            if (_currentPage.HasValue)
                ExitPage(_currentPage.Value);

            var previous = _history.Pop();
            EnterPage(previous, null);
            _currentPage = previous;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void EnterPage(PageType pageType, object param)
        {
            if (!_pageMap.TryGetValue(pageType, out var handler))
            {
                Debug.LogError($"[UIManager] No handler registered for {pageType}.");
                return;
            }
            ((MonoBehaviour)handler).gameObject.SetActive(true);
            handler.OnEnter(param);
        }

        private void ExitPage(PageType pageType)
        {
            if (!_pageMap.TryGetValue(pageType, out var handler)) return;
            handler.OnExit();
            ((MonoBehaviour)handler).gameObject.SetActive(false);
        }
    }
}
