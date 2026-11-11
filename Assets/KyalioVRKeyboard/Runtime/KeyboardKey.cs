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
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Optional. Shown instead of the label when the keyboard gives this key an icon.")]
        [SerializeField] private Image icon;

        [Header("Colors (multiplied with the background Image color)")]
        [SerializeField] private Color normalColor      = Color.white;
        [SerializeField] private Color highlightedColor = new(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color pressedColor     = new(0.7f, 0.7f, 0.7f, 1f);
        [Tooltip("Shift (and caps lock) while on.")]
        [SerializeField] private Color activeColor      = new(0.55f, 0.75f, 1f, 1f);

        internal event Action<KeyboardKey> Pressed;    // pointer down
        internal event Action<KeyboardKey> Released;   // pointer up
        internal event Action<KeyboardKey> Clicked;    // released over the key

        internal KeyDef Def { get; private set; }

        private bool _hovered, _down, _active;

        public RectTransform Rect => (RectTransform)transform;

        internal void Setup(KeyDef def, string text, Sprite iconSprite)
        {
            Def = def;
            SetLabel(text, iconSprite);
            // Keep _hovered: a page switch reuses the key under the pointer, which is still
            // hovering it (OnDisable clears hover for keys that are hidden).
            _down = false;
            SetActive(false);
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

        /// <summary>Lit state for Shift / caps lock.</summary>
        internal void SetActive(bool active)
        {
            _active = active;
            Refresh();
        }

        private void Refresh()
        {
            if (background == null) return;
            background.canvasRenderer.SetColor(
                _down ? pressedColor : _active ? activeColor : _hovered ? highlightedColor : normalColor);
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
    }
}
