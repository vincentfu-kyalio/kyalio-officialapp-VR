using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// A ScrollRect intended for use as a horizontal scroll inside a vertical page scroll.
    ///
    /// On every drag-begin, it measures the initial movement direction:
    ///   - Predominantly vertical → forwards all drag events to the nearest ancestor ScrollRect
    ///     (the page's vertical scroll), so the page scrolls as expected.
    ///   - Predominantly horizontal → behaves exactly like a normal ScrollRect.
    ///
    /// Usage: in the Inspector, replace the ScrollRect component on any horizontal scroll
    /// container with this component. All serialised fields (Content, Viewport, etc.) are
    /// inherited from ScrollRect and work identically.
    /// </summary>
    public class NestedScrollRect : ScrollRect
    {
        // Minimum drag magnitude (pixels) required before direction can be determined.
        // Below this threshold the first non-zero delta is trusted directly.
        private const float DirectionThreshold = 0.5f;

        private ScrollRect _parentScrollRect;
        private bool _routeToParent;

        protected override void Awake()
        {
            base.Awake();

            // Walk up the hierarchy to find the nearest ancestor ScrollRect.
            var t = transform.parent;
            while (t != null)
            {
                _parentScrollRect = t.GetComponent<ScrollRect>();
                if (_parentScrollRect != null) break;
                t = t.parent;
            }
        }

        // Must also initialise the parent so it is ready to receive drag events.
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            _parentScrollRect?.OnInitializePotentialDrag(eventData);
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (_parentScrollRect == null)
            {
                base.OnBeginDrag(eventData);
                return;
            }

            // Decide direction. eventData.delta is the movement since the last frame;
            // by the time OnBeginDrag fires the pointer has already exceeded the drag
            // threshold, so the delta is reliably non-zero.
            var delta = eventData.delta;
            bool verticalDominates = delta.magnitude > DirectionThreshold
                ? Mathf.Abs(delta.y) >= Mathf.Abs(delta.x)
                : false; // ambiguous — default to horizontal (own scroll)

            _routeToParent = verticalDominates;

            if (_routeToParent)
                _parentScrollRect.OnBeginDrag(eventData);
            else
                base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_routeToParent && _parentScrollRect != null)
                _parentScrollRect.OnDrag(eventData);
            else
                base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_routeToParent && _parentScrollRect != null)
                _parentScrollRect.OnEndDrag(eventData);
            else
                base.OnEndDrag(eventData);

            _routeToParent = false;
        }
    }
}
