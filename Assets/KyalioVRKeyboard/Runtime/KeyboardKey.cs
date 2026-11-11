using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kyalio.VRKeyboard
{
    /// <summary>
    /// One key. Put it on the key template (a background Image with a TMP label child and,
    /// optionally, an icon Image child); <see cref="VirtualKeyboard"/> clones the template
    /// for every key.
    ///
    /// Deliberately not a Button / Selectable: input modules deselect the current selection
    /// when the pressed object has a different select handler. A key has none, so the event
    /// system walks up to the input field the keyboard is parented under — already selected —
    /// and the field keeps its focus, caret and caret position.
    ///
    /// Drag events are swallowed so a drag across keys never reaches the field (it would
    /// start a text selection).
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyboardKey : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IScrollHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Optional. Shown instead of the label when the keyboard gives this key an icon.")]
        [SerializeField] private Image icon;

        [Tooltip("Optional. Shown only while caps lock is on (e.g. a small dot or bar), so caps lock " +
                 "reads differently from a one-shot Shift.")]
        [SerializeField] private GameObject lockIndicator;

        // The background Image's color per state (set directly, so each state is exactly the
        // color shown; style function / accent keys by giving their templates other colors).
        [Header("Background colors")]
        [SerializeField] private Color normalColor      = new(0.26f, 0.27f, 0.30f, 1f);
        [Tooltip("Pointer over the key: shows where the ray is before pressing.")]
        [SerializeField] private Color highlightedColor = new(0.36f, 0.38f, 0.43f, 1f);
        [SerializeField] private Color pressedColor     = new(0.18f, 0.19f, 0.21f, 1f);
        [Tooltip("Shift while on for the next letter.")]
        [SerializeField] private Color activeColor      = new(0.31f, 0.47f, 0.78f, 1f);
        [Tooltip("Shift while caps lock is on.")]
        [SerializeField] private Color lockedColor      = new(0.20f, 0.42f, 0.90f, 1f);

        [Header("Press")]
        [Tooltip("Scale while pressed, so a press is visible even without a physical key.")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.94f;

        internal event Action<KeyboardKey> Pressed;    // pointer down
        internal event Action<KeyboardKey> Released;   // pointer up
        internal event Action<KeyboardKey> Clicked;    // released over the key

        internal KeyDef Def { get; private set; }

        private bool _hovered, _down, _active, _locked;

        public RectTransform Rect => (RectTransform)transform;

        internal void Setup(KeyDef def, string text, Sprite iconSprite)
        {
            Def = def;
            SetLabel(text, iconSprite);
            // Keep _hovered: a page switch reuses the key under the pointer, which is still
            // hovering it (OnDisable clears hover for keys that are hidden).
            _down = false;
            SetToggle(false, false);
        }

        internal void SetLabel(string text, Sprite iconSprite = null)
        {
            bool useIcon = icon != null && iconSprite != null;
            if (icon != null)
            {
                icon.gameObject.SetActive(useIcon);
                if (useIcon) icon.sprite = iconSprite;
            }
            if (label != null)
            {
                label.gameObject.SetActive(!useIcon);
                label.text = text ?? string.Empty;
            }
        }

        /// <summary>Shift state: on (next letter upper case) and locked (caps lock).</summary>
        internal void SetToggle(bool active, bool locked)
        {
            _active = active || locked;
            _locked = locked;
            if (lockIndicator != null) lockIndicator.SetActive(locked);
            Refresh();
        }

        private void Refresh()
        {
            if (background != null)
                background.color =
                    _down    ? pressedColor :
                    _locked  ? lockedColor :
                    _active  ? activeColor :
                    _hovered ? highlightedColor : normalColor;
            transform.localScale = _down ? new Vector3(pressedScale, pressedScale, 1f) : Vector3.one;
        }

        private void OnDisable()
        {
            _hovered = _down = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData e)
        {
            _down = true;
            Refresh();
            Pressed?.Invoke(this);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _down = false;
            Refresh();
            Released?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);

        public void OnPointerEnter(PointerEventData e) { _hovered = true;  Refresh(); }
        public void OnPointerExit(PointerEventData e)  { _hovered = false; Refresh(); }

        // Swallow drags (see class summary). Disabling the threshold check is not needed;
        // the handlers only have to exist so the drag stops at the key.
        public void OnInitializePotentialDrag(PointerEventData e) { }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { }

        // Swallow scroll too: it would scroll the field's text or a ScrollRect behind it.
        public void OnScroll(PointerEventData e) { }
    }
}
