using System.Collections.Generic;
using Kyalio.Models.V2;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Virtualised project card grid: only the rows inside the scroll viewport (plus a
    /// buffer row above and below) have a card, so showing ~165 projects costs about a
    /// dozen cards instead of one per project. Cards scrolled out are rebound to the
    /// rows scrolling in.
    ///
    /// The container's GridLayoutGroup stays the single source of the layout (cell size,
    /// spacing, padding, alignment, column constraint) but is disabled at runtime; cards
    /// are placed in code. A LayoutElement on the container reports the full grid height,
    /// so its ContentSizeFitter and the ScrollRect still size and scroll as if every card
    /// existed.
    ///
    /// Attach to the container that holds the ScrollRect.
    /// Inspector: cardPrefab, container (the GridLayoutGroup), scrollRect
    /// </summary>
    public class ProjectCardList : MonoBehaviour
    {
        [SerializeField] private ProjectCard cardPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Extra rows kept bound above and below the viewport.")]
        [SerializeField, Min(0)] private int bufferRows = 1;

        // Click callback, set by the page script
        public System.Action<Project> OnProjectClicked;

        private readonly List<Project> _projects = new();
        private readonly Dictionary<int, ProjectCard> _shown = new();   // data index → card
        private readonly List<ProjectCard> _spare = new();              // inactive, reusable
        private readonly List<int> _scratch = new();
        private readonly Vector3[] _corners = new Vector3[4];

        private bool _initialized;
        private RectTransform _rect;            // the grid container
        private GridLayoutGroup _grid;
        private LayoutElement _heightElement;
        private ScrollRect _scroller;           // nearest ScrollRect above the grid

        private int _columns = 1;
        private float _layoutWidth = -1f;
        private int _firstShown = -1, _lastShown = -1;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Displays the list of projects and scrolls to the top.
        /// Pass keepScroll when re-showing the same projects (e.g. returning to a page):
        /// the content position is left untouched, so the user lands where they left.
        /// </summary>
        public void Show(IReadOnlyList<Project> projects, bool keepScroll = false)
        {
            Init();

            _projects.Clear();
            if (projects != null) _projects.AddRange(projects);

            ApplyGridHeight();
            RefreshLayout(keepScroll);
            Refresh(force: true);
        }

        /// <summary>Removes every card from view.</summary>
        public void Clear() => Show(null, keepScroll: true);

        // ── Setup ─────────────────────────────────────────────────────

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _rect = (RectTransform)container;
            _grid = container.GetComponent<GridLayoutGroup>();
            if (_grid != null) _grid.enabled = false;   // cards are placed in code

            if (!container.TryGetComponent(out _heightElement))
                _heightElement = container.gameObject.AddComponent<LayoutElement>();

            // Search nests the grid in an inner ScrollRect under the serialized one; the inner
            // one does the scrolling, but either moving shifts what is in view.
            _scroller = container.GetComponentInParent<ScrollRect>(includeInactive: true);
            if (_scroller != null) _scroller.onValueChanged.AddListener(OnScrolled);
            if (scrollRect != null && scrollRect != _scroller)
                scrollRect.onValueChanged.AddListener(OnScrolled);
        }

        private void OnScrolled(Vector2 _) => Refresh();

        private void OnDestroy()
        {
            if (_scroller != null) _scroller.onValueChanged.RemoveListener(OnScrolled);
            if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrolled);
        }

        // ── Layout math (mirrors GridLayoutGroup, upper-left start corner, horizontal axis) ──

        private Vector2 Cell    => _grid != null ? _grid.cellSize : ((RectTransform)cardPrefab.transform).sizeDelta;
        private Vector2 Spacing => _grid != null ? _grid.spacing  : Vector2.zero;
        private RectOffset Pad  => _grid != null ? _grid.padding  : new RectOffset();

        private int Rows => _columns > 0 ? (_projects.Count + _columns - 1) / _columns : 0;

        private int ComputeColumns(float width)
        {
            if (_grid != null && _grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                return Mathf.Max(1, _grid.constraintCount);
            float usable = width - Pad.horizontal + Spacing.x;
            return Mathf.Max(1, Mathf.FloorToInt((usable + 0.001f) / (Cell.x + Spacing.x)));
        }

        private void ApplyGridHeight()
        {
            _layoutWidth = _rect.rect.width;
            _columns = ComputeColumns(_layoutWidth);

            int rows = Rows;
            float height = rows == 0
                ? Pad.vertical
                : Pad.vertical + rows * Cell.y + (rows - 1) * Spacing.y;
            _heightElement.preferredHeight = height;
            _heightElement.minHeight = height;
        }

        private Vector2 CellTopLeft(int index)
        {
            int row = index / _columns, col = index % _columns;
            float gridWidth = _columns * Cell.x + (_columns - 1) * Spacing.x + Pad.horizontal;
            float alignX = _grid != null ? (int)_grid.childAlignment % 3 * 0.5f : 0f;
            float offsetX = (_layoutWidth - gridWidth) * alignX;

            return new Vector2(
                offsetX + Pad.left + col * (Cell.x + Spacing.x),
                -(Pad.top + row * (Cell.y + Spacing.y)));
        }

        // ── Visible range ─────────────────────────────────────────────

        /// <summary>Data index range whose rows overlap the viewport (± bufferRows).</summary>
        private bool VisibleRange(out int first, out int last)
        {
            first = 0; last = -1;
            if (_projects.Count == 0) return false;

            var viewport = _scroller != null
                ? (_scroller.viewport != null ? _scroller.viewport : (RectTransform)_scroller.transform)
                : _rect;
            viewport.GetWorldCorners(_corners);

            // Viewport top/bottom as distance below the grid's top edge, in grid space.
            float top = _rect.rect.yMax;
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var corner in _corners)
            {
                float y = _rect.InverseTransformPoint(corner).y;
                yMin = Mathf.Min(yMin, y);
                yMax = Mathf.Max(yMax, y);
            }
            float fromTop = top - yMax, toBottom = top - yMin;

            float rowStep = Cell.y + Spacing.y;
            int firstRow = Mathf.FloorToInt((fromTop - Pad.top) / rowStep) - bufferRows;
            int lastRow  = Mathf.FloorToInt((toBottom - Pad.top) / rowStep) + bufferRows;
            firstRow = Mathf.Clamp(firstRow, 0, Rows - 1);
            lastRow  = Mathf.Clamp(lastRow, 0, Rows - 1);
            if (lastRow < firstRow) return false;

            first = firstRow * _columns;
            last  = Mathf.Min(_projects.Count - 1, (lastRow + 1) * _columns - 1);
            return true;
        }

        /// <summary>
        /// Rebinds cards to the rows now in view. With force (new data / layout) every shown
        /// card is rebound; otherwise only rows that entered or left the range change.
        /// </summary>
        private void Refresh(bool force = false)
        {
            if (!_initialized) return;

            if (!Mathf.Approximately(_rect.rect.width, _layoutWidth))
            {
                ApplyGridHeight();
                force = true;
            }

            bool any = VisibleRange(out int first, out int last);
            if (!force && first == _firstShown && last == _lastShown) return;

            // Free cards outside the new range.
            _scratch.Clear();
            foreach (var kv in _shown)
                if (!any || kv.Key < first || kv.Key > last)
                    _scratch.Add(kv.Key);
            foreach (int index in _scratch)
            {
                _spare.Add(_shown[index]);
                _shown.Remove(index);
            }

            if (any)
            {
                for (int i = first; i <= last; i++)
                {
                    // A card already at this index keeps its slot; when forced it is rebound
                    // (ProjectCard.Bind returns early if the project is unchanged).
                    if (_shown.TryGetValue(i, out var card))
                    {
                        if (!force) continue;
                    }
                    else
                    {
                        card = TakeCard();
                        _shown[i] = card;
                    }
                    Place(card, i);
                    card.Bind(_projects[i]);
                }
            }

            // Leftovers stay pooled but hidden; normally zero once the list is in view.
            foreach (var card in _spare)
                if (card.gameObject.activeSelf)
                {
                    card.CancelLoads();
                    card.gameObject.SetActive(false);
                }

            _firstShown = any ? first : -1;
            _lastShown  = any ? last : -1;
        }

        private ProjectCard TakeCard()
        {
            ProjectCard card;
            if (_spare.Count > 0)
            {
                // Prefer a still-active spare: reusing it avoids an OnDisable/OnEnable rebuild.
                int pick = _spare.FindLastIndex(c => c.gameObject.activeSelf);
                if (pick < 0) pick = _spare.Count - 1;
                card = _spare[pick];
                _spare.RemoveAt(pick);
            }
            else
            {
                card = Instantiate(cardPrefab, container);
                card.OnClicked = p => OnProjectClicked?.Invoke(p);
            }

            if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
            return card;
        }

        private void Place(ProjectCard card, int index)
        {
            var rt = (RectTransform)card.transform;
            var cell = Cell;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // grid's top-left corner
            rt.sizeDelta = cell;
            var topLeft = CellTopLeft(index);
            rt.anchoredPosition = new Vector2(
                topLeft.x + rt.pivot.x * cell.x,
                topLeft.y - (1f - rt.pivot.y) * cell.y);
        }

        /// <summary>
        /// Rebuilds the scroll content after the grid height changes and, unless keepScroll,
        /// resets the scroll position to the top. The grid is nested inside the ScrollRect
        /// content, so rebuilding the outer content is what propagates the new height up to
        /// the ScrollRect — rebuilding only the grid leaves the scroll view blank or clipped.
        /// </summary>
        private void RefreshLayout(bool keepScroll)
        {
            // Inner (grid's own) ScrollRect first, then the serialized outer one if different.
            RebuildScroll(_scroller, keepScroll);
            if (scrollRect != _scroller) RebuildScroll(scrollRect, keepScroll);
        }

        private static void RebuildScroll(ScrollRect sr, bool keepScroll)
        {
            if (sr == null || sr.content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
            if (!keepScroll) sr.verticalNormalizedPosition = 1f;
        }
    }
}
