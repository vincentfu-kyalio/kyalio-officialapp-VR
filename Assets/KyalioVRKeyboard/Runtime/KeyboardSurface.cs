using UnityEngine;
using UnityEngine.EventSystems;

namespace Kyalio.VRKeyboard
{
    /// <summary>
    /// Catches presses on the keyboard's background (between keys). Without it they would
    /// bubble up to the input field the keyboard is parented under, which would move the
    /// caret or start a selection. Added by <see cref="VirtualKeyboard"/> to its root.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyboardSurface : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnPointerDown(PointerEventData e) { }
        public void OnPointerUp(PointerEventData e) { }
        public void OnPointerClick(PointerEventData e) { }
        public void OnInitializePotentialDrag(PointerEventData e) { }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { }
    }
}
