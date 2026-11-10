using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    /// All registered pages are hidden on Awake; UIManager controls visibility.
    ///
    /// Pages flagged hideWithCanvas stay active and are hidden by disabling their own
    /// nested Canvas instead. Toggling a GameObject re-runs OnEnable/OnDisable on every
    /// graphic below it, which rebuilds all of its meshes and layouts — a visible hitch
    /// on content-heavy pages. Disabling a Canvas only stops drawing it. Flag pages
    /// with no per-frame logic of their own (e.g. the tab pages); a flagged page also
    /// runs Awake at startup instead of on first visit.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private PageEntry[] pages;

        [Tooltip("Longest time a faded page switch stays black waiting for an IRevealGate page " +
                 "(e.g. the video's first frame) before fading in anyway.")]
        [SerializeField, Min(0f)] private float maxRevealWait = 3f;

        [Tooltip("Fade-in time of a page entered without the black SceneFader fade. 0 turns it off.")]
        [SerializeField, Min(0f)] private float pageFadeInDuration = 0.15f;

        private readonly Dictionary<PageType, Coroutine> _pageFades = new();
        private readonly Dictionary<PageType, CanvasGroup> _fadeGroups = new();

        [System.Serializable]
        private class PageEntry
        {
            public PageType pageType;
            public GameObject pageObject;
            [Tooltip("Hide by disabling a nested Canvas instead of deactivating the GameObject.")]
            public bool hideWithCanvas;
        }

        private class CanvasToggle
        {
            public Canvas Canvas;
            public GraphicRaycaster Raycaster;
        }

        private readonly Dictionary<PageType, IPageHandler> _pageMap = new();
        private readonly Dictionary<PageType, CanvasToggle> _canvasPages = new();
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
                    if (entry.hideWithCanvas)
                    {
                        var toggle = CreateCanvasToggle(entry.pageObject);
                        _canvasPages[entry.pageType] = toggle;
                        SetCanvasVisible(toggle, false);
                        entry.pageObject.SetActive(true);
                    }
                    else
                    {
                        entry.pageObject.SetActive(false);
                    }
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
        /// A page implementing <see cref="IRevealGate"/> keeps the screen black until it
        /// is ready (at most maxRevealWait seconds).
        /// </summary>
        public void GoTo(PageType pageType, object param = null, bool fade = false)
        {
            if (fade && SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOutThenIn(
                    () => BehindFade(() => GoToImmediate(pageType, param)),
                    holdWhile: () => !IsReadyToReveal(pageType),
                    maxHold: maxRevealWait);
                return;
            }
            GoToImmediate(pageType, param);
        }

        // ── Page fade-in ──────────────────────────────────────────────

        // A page switch hidden behind the black SceneFader fade is already covered;
        // fading the page in as well would only delay it.
        private bool _enteringBehindFade;

        private void BehindFade(System.Action enter)
        {
            _enteringBehindFade = true;
            try     { enter(); }
            finally { _enteringBehindFade = false; }
        }

        // Fades the entered page from transparent, so content bound in OnEnter (and
        // layout settling in its first frames) appears as one smooth reveal.
        private void FadeInPage(PageType pageType, GameObject page)
        {
            StopPageFade(pageType);

            // A page that brings its own CanvasGroup (e.g. the video controls' show/hide)
            // drives that alpha itself; only groups added here are ours to animate.
            if (!_fadeGroups.TryGetValue(pageType, out var group))
            {
                if (page.TryGetComponent<CanvasGroup>(out _)) return;
                group = page.AddComponent<CanvasGroup>();
                _fadeGroups[pageType] = group;
            }

            if (_enteringBehindFade || pageFadeInDuration <= 0f)
            {
                group.alpha = 1f;
                return;
            }
            group.alpha = 0f;
            _pageFades[pageType] = StartCoroutine(FadeInRoutine(pageType, group));
        }

        private void StopPageFade(PageType pageType)
        {
            if (!_pageFades.TryGetValue(pageType, out var running)) return;
            if (running != null) StopCoroutine(running);
            _pageFades.Remove(pageType);
        }

        private System.Collections.IEnumerator FadeInRoutine(PageType pageType, CanvasGroup group)
        {
            float elapsed = 0f;
            while (elapsed < pageFadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / pageFadeInDuration);
                yield return null;
            }
            group.alpha = 1f;
            _pageFades.Remove(pageType);
        }

        // Pages without a gate are ready as soon as they are entered.
        private bool IsReadyToReveal(PageType pageType) =>
            !_pageMap.TryGetValue(pageType, out var handler) ||
            handler is not IRevealGate gate ||
            gate.IsReadyToReveal;

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
                SceneFader.Instance.FadeOutThenIn(() => BehindFade(GoBackImmediate));
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
            var page = ((MonoBehaviour)handler).gameObject;
            if (_canvasPages.TryGetValue(pageType, out var toggle))
                SetCanvasVisible(toggle, true);
            else
                page.SetActive(true);
            handler.OnEnter(param);

            // After OnEnter: the page is filled while still fully transparent.
            FadeInPage(pageType, page);
        }

        private void ExitPage(PageType pageType)
        {
            if (!_pageMap.TryGetValue(pageType, out var handler)) return;
            StopPageFade(pageType);
            handler.OnExit();
            if (_canvasPages.TryGetValue(pageType, out var toggle))
                SetCanvasVisible(toggle, false);
            else
                ((MonoBehaviour)handler).gameObject.SetActive(false);
        }

        /// <summary>
        /// Gives the page its own nested Canvas (drawn in hierarchy order, like before) and a
        /// GraphicRaycaster, since a GraphicRaycaster only hits graphics of its own Canvas.
        /// </summary>
        private static CanvasToggle CreateCanvasToggle(GameObject page)
        {
            var parent = page.transform.parent != null
                ? page.transform.parent.GetComponentInParent<Canvas>(includeInactive: true)
                : null;

            if (!page.TryGetComponent<Canvas>(out var canvas))
                canvas = page.AddComponent<Canvas>();
            if (parent != null)
                canvas.additionalShaderChannels = parent.additionalShaderChannels; // TMP needs these

            if (!page.TryGetComponent<GraphicRaycaster>(out var raycaster))
            {
                raycaster = page.AddComponent<GraphicRaycaster>();
                if (parent != null && parent.TryGetComponent<GraphicRaycaster>(out var parentRaycaster))
                {
                    raycaster.ignoreReversedGraphics = parentRaycaster.ignoreReversedGraphics;
                    raycaster.blockingObjects = parentRaycaster.blockingObjects;
                }
            }

            return new CanvasToggle { Canvas = canvas, Raycaster = raycaster };
        }

        private static void SetCanvasVisible(CanvasToggle toggle, bool visible)
        {
            toggle.Canvas.enabled = visible;
            toggle.Raycaster.enabled = visible;
        }
    }
}
