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
            public MonoBehaviour handler;
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
                if (entry.handler is IPageHandler pageHandler)
                {
                    _pageMap[entry.pageType] = pageHandler;
                    entry.handler.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"[UIManager] {entry.handler?.name} does not implement IPageHandler.", entry.handler);
                }
            }
        }

        /// <summary>
        /// Navigate to a page, pushing the current page onto the history stack.
        /// Pass null for param when no data needs to be forwarded.
        /// </summary>
        public void GoTo(PageType pageType, object param = null)
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
        /// </summary>
        public void GoBack()
        {
            if (_history.Count == 0)
            {
                Debug.LogWarning("[UIManager] GoBack called with empty history.");
                return;
            }

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
