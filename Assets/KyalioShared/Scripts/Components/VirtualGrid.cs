using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kyalio.Components
{
    /// <summary>
    /// Virtualised card grid: only the rows inside the scroll viewport (plus buffer rows
    /// above and below) have a card, so a list of hundreds costs about a dozen cards.
    /// Cards scrolled out are rebound to the rows scrolling in.
    ///
    /// The container's GridLayoutGroup stays the single source of the layout (cell size,
    /// spacing, padding, alignment, column constraint) but is disabled; cards are placed
    /// in code. A LayoutElement on the container reports the full grid height, so its
    /// ContentSizeFitter and the ScrollRect size and scroll as if every card existed.
    ///
    /// Several grids may share one container (e.g. two card types, one shown at a time):
    /// set the hidden one's count to 0 before showing the other.
    /// </summary>
    public class VirtualGrid<T> : IDisposable where T : Component
    {
        private readonly RectTransform _rect;
        private readonly T _prefab;
        private readonly GridLayoutGroup _grid;
        private readonly LayoutElement _heightElement;
        private readonly bool _sizeSelf;            // no ContentSizeFitter: set the rect height ourselves
        private readonly ScrollRect _scroller;      // nearest ScrollRect above the grid
        private readonly ScrollRect _outer;         // optional extra (serialized) ScrollRect
        private readonly int _bufferRows;
        private readonly Action<T> _onCreate;
        private readonly Action<T, int> _bind;
        private readonly Action<T> _onHide;

        private readonly Dictionary<int, T> _shown = new();   // data index → card
        private readonly List<T> _spare = new();
        private readonly List<int> _scratch = new();
        private readonly Vector3[] _corners = new Vector3[4];

        private int _count;
        private int _columns = 1;
        private float _layoutWidth = -1f;
        private int _firstShown = -1, _lastShown = -1;

        /// <param name="container">The RectTransform holding the GridLayoutGroup.</param>
        /// <param name="outerScroll">A ScrollRect besides the grid's nearest one whose movement also changes what is in view (may be null or the same).</param>
        /// <param name="onCreate">Runs once per new card (e.g. hook its click callback).</param>
        /// <param name="bind">Binds data index → card; called when a card takes a row, and for every shown card on <see cref="SetCount"/>.</param>
        /// <param name="onHide">Runs when a card is pooled hidden (e.g. cancel its image loads).</param>
        public VirtualGrid(RectTransform container, T prefab, ScrollRect outerScroll, int bufferRows,
            Action<T> onCreate, Action<T, int> bind, Action<T> onHide)
        {
            _rect = container;
            _prefab = prefab;
            _bufferRows = Mathf.Max(0, bufferRows);
            _onCreate = onCreate;
            _bind = bind;
            _onHide = onHide;

            _grid = container.GetComponent<GridLayoutGroup>();
            if (_grid != null) _grid.enabled = false;   // cards are placed in code

            if (!container.TryGetComponent(out _heightElement))
                _heightElement = container.gameObject.AddComponent<LayoutElement>();
            _sizeSelf = !container.TryGetComponent<ContentSizeFitter>(out _);

            _scroller = container.GetComponentInParent<ScrollRect>(includeInactive: true);
            _outer = outerScroll != _scroller ? outerScroll : null;
            if (_scroller != null) _scroller.onValueChanged.AddListener(OnScrolled);
            if (_outer != null) _outer.onValueChanged.AddListener(OnScrolled);
        }

        public int Count => _count;

        /// <summary>
        /// Sets how many items the grid shows, resizes it, rebinds the cards in view and,
        /// unless keepScroll, scrolls to the top. Call after the bound data changed.
        /// </summary>
        public void SetCount(int count, bool keepScroll)
        {
            _count = Mathf.Max(0, count);
            ApplyGridHeight();
            RebuildScroll(_scroller, keepScroll);
            RebuildScroll(_outer, keepScroll);
            Refresh(force: true);
        }

        /// <summary>Rebinds the cards in view without changing the count or the layout (e.g. after a selection change).</summary>
        public void RebindShown() => Refresh(force: true);

        public void Dispose()
        {
            if (_scroller != null) _scroller.onValueChanged.RemoveListener(OnScrolled);
            if (_outer != null) _outer.onValueChanged.RemoveListener(OnScrolled);
        }

        private void OnScrolled(Vector2 _) => Refresh();

        // ── Layout math (mirrors GridLayoutGroup, upper-left start corner, horizontal axis) ──

        private Vector2 Cell    => _grid != null ? _grid.cellSize : ((RectTransform)_prefab.transform).sizeDelta;
        private Vector2 Spacing => _grid != null ? _grid.spacing  : Vector2.zero;
        private RectOffset Pad  => _grid != null ? _grid.padding  : new RectOffset();

        private int Rows => (_count + _columns - 1) / _columns;

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
            // Read by a parent layout that controls child height, or by the container's own
            // ContentSizeFitter. Without either (a fixed-height grid) set the height directly,
            // otherwise items past the fixed height could never be scrolled to.
            if (_sizeSelf)
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
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

        /// <summary>Data index range whose rows overlap the viewport (± buffer rows).</summary>
        private bool VisibleRange(out int first, out int last)
        {
            first = 0; last = -1;
            if (_count == 0) return false;

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
            int firstRow = Mathf.FloorToInt((fromTop - Pad.top) / rowStep) - _bufferRows;
            int lastRow  = Mathf.FloorToInt((toBottom - Pad.top) / rowStep) + _bufferRows;
            firstRow = Mathf.Clamp(firstRow, 0, Rows - 1);
            lastRow  = Mathf.Clamp(lastRow, 0, Rows - 1);
            if (lastRow < firstRow) return false;

            first = firstRow * _columns;
            last  = Mathf.Min(_count - 1, (lastRow + 1) * _columns - 1);
            return true;
        }

        /// <summary>
        /// Rebinds cards to the rows now in view. With force (new data / layout) every shown
        /// card is rebound; otherwise only rows that entered or left the range change.
        /// </summary>
        private void Refresh(bool force = false)
        {
            // An empty grid sharing its container must not overwrite the other grid's height.
            if (_count > 0 && !Mathf.Approximately(_rect.rect.width, _layoutWidth))
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
                    // A card already at this index keeps its slot; when forced it is rebound.
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
                    _bind(card, i);
                }
            }

            // Leftovers stay pooled but hidden; normally zero once the list is in view.
            foreach (var card in _spare)
                if (card.gameObject.activeSelf)
                {
                    _onHide?.Invoke(card);
                    card.gameObject.SetActive(false);
                }

            _firstShown = any ? first : -1;
            _lastShown  = any ? last : -1;
        }

        private T TakeCard()
        {
            T card;
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
                card = Object.Instantiate(_prefab, _rect);
                _onCreate?.Invoke(card);
            }

            if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
            return card;
        }

        private void Place(T card, int index)
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
        private static void RebuildScroll(ScrollRect sr, bool keepScroll)
        {
            if (sr == null || sr.content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
            if (!keepScroll) sr.verticalNormalizedPosition = 1f;
        }
    }
}
