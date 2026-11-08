using TMPro;
using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Makes a TMP_InputField usable with the Quest system keyboard while the UI lives
    /// under an OVROverlayCanvas.
    ///
    ///   • Overlay suspend — with the canvas in PunchAHole (underlay) mode the system
    ///     keyboard never shows up. While any field using this component is focused the
    ///     overlay is turned off (the canvas falls back to its imposter mesh, same trick as
    ///     PlayVideoPage) and restored once the last one loses focus.
    ///
    /// Known limitation: with the GameActivity entry point the keyboard's own preview box
    /// doesn't follow typing (the field does). Writing TouchScreenKeyboard.text back does
    /// not refresh it, and the Activity entry point can't be built for this project.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public class SystemKeyboardInput : MonoBehaviour
    {
        // Shared across fields: switching from one field to another must not flash the
        // overlay back on in between.
        private static int s_holdCount;
        private static OVROverlayCanvas s_heldCanvas;
        private static bool s_restoreValue;

        private TMP_InputField _field;
        private OVROverlayCanvas _canvas;
        private bool _holding;
        private int _holdFrame;

        /// <summary>Adds the component to a field that doesn't have it yet.</summary>
        public static void EnsureOn(TMP_InputField field)
        {
            if (field != null && field.GetComponent<SystemKeyboardInput>() == null)
                field.gameObject.AddComponent<SystemKeyboardInput>();
        }

        private void Awake()
        {
            _field = GetComponent<TMP_InputField>();
            _canvas = GetComponentInParent<OVROverlayCanvas>(includeInactive: true);
            // Select fires a frame before the keyboard opens — suspend the overlay early.
            _field.onSelect.AddListener(OnFieldSelected);
        }

        private void OnDestroy()
        {
            if (_field != null) _field.onSelect.RemoveListener(OnFieldSelected);
        }

        private void OnFieldSelected(string _) => Hold();

        private void OnDisable() => Release();

        private void Update()
        {
            // Polled rather than event-driven: after the keyboard closes the field stays
            // selected (no onSelect on the next tap) and onDeselect doesn't fire.
            // TMP activates in the LateUpdate after onSelect, so give it a frame before
            // treating "not focused" as "done typing".
            if (_field.isFocused) Hold();
            else if (_holding && Time.frameCount - _holdFrame > 1) Release();
        }

        private void Hold()
        {
            if (_holding || _canvas == null) return;
            _holding = true;
            _holdFrame = Time.frameCount;

            if (s_holdCount++ == 0)
            {
                s_heldCanvas = _canvas;
                s_restoreValue = _canvas.overlayEnabled;
                _canvas.overlayEnabled = false;
            }
        }

        private void Release()
        {
            if (!_holding) return;
            _holding = false;

            if (--s_holdCount == 0 && s_heldCanvas != null)
            {
                s_heldCanvas.overlayEnabled = s_restoreValue;
                s_heldCanvas = null;
            }
        }
    }
}
