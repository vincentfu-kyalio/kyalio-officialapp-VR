using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kyalio.VRKeyboard
{
    /// <summary>
    /// In-world keyboard for TMP_InputFields on a world-space canvas (VR).
    ///
    /// Register each field with <see cref="Register"/>. While a registered field is focused
    /// the keyboard shows at its dock and types into it; the field keeps its own blinking
    /// caret, and pointing into the text moves the caret (both standard TMP_InputField
    /// behaviour, since the field never loses focus). The platform soft keyboard is turned
    /// off for registered fields.
    ///
    /// How focus is kept: while shown, the keyboard is parented under the focused field
    /// (placed at the dock's pose, drawn by its own nested Canvas so nothing covers it and no
    /// parent mask clips it). A press on a key — which is not a Selectable — makes the event
    /// system look up the hierarchy for a select handler, find the already-selected field
    /// and leave the selection alone. Typing goes through TMP_InputField.ProcessEvent, so
    /// content type, validation, character limit and selection replacement all apply.
    ///
    /// Scene setup (see README): this component on an object that stays active (it polls
    /// focus every frame), with a child panel ? a RectTransform with the background Image,
    /// sized as the keyboard ? holding the key area and an inactive key template with
    /// <see cref="KeyboardKey"/>. Only the panel moves under the focused field; it hides by
    /// disabling its own Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class VirtualKeyboard : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The visible keyboard: background Image, sized as the keyboard. A child of this object.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("Where the keys are laid out (inside the panel, inset for padding).")]
        [SerializeField] private RectTransform keyArea;
        [Tooltip("Key template, cloned for every key. Kept inactive.")]
        [SerializeField] private KeyboardKey keyTemplate;
        [Tooltip("Where the keyboard appears (its center) unless a field registers its own dock.")]
        [SerializeField] private RectTransform defaultDock;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float spacing = 12f;
        [Tooltip("Sorting order of the keyboard's own Canvas, so it draws above the page.")]
        [SerializeField] private int sortingOrder = 100;

        [Header("Labels")]
        [SerializeField] private string backspaceLabel = "Del";
        [SerializeField] private string shiftLabel = "Shift";
        [SerializeField] private string doneLabel = "Done";
        [SerializeField] private string spaceLabel = "Space";

        [Header("Icons (optional; replace the label when set)")]
        [SerializeField] private Sprite backspaceIcon;
        [SerializeField] private Sprite shiftIcon;
        [SerializeField] private Sprite doneIcon;

        [Header("Behaviour")]
        [Tooltip("Hold Backspace this long before it repeats.")]
        [SerializeField, Min(0f)] private float repeatDelay = 0.45f;
        [SerializeField, Min(0.01f)] private float repeatInterval = 0.07f;
        [Tooltip("Two Shift taps within this time turn on caps lock.")]
        [SerializeField, Min(0f)] private float capsLockDoubleTap = 0.35f;

        /// <summary>Done was pressed on this field (the keyboard then closes).</summary>
        public event Action<TMP_InputField> DonePressed;

        /// <summary>Any key acted on (for sounds or haptics).</summary>
        public event Action KeyPressed;

        /// <summary>The field being typed into, or null while hidden.</summary>
        public TMP_InputField Target => _target;

        private sealed class Registration
        {
            public KeyboardLayoutKind Kind;
            public RectTransform Dock;
        }

        private readonly Dictionary<TMP_InputField, Registration> _fields = new();
        private readonly List<KeyboardKey> _keys = new();
        // Created on first use: Event's setters may not run in a MonoBehaviour constructor
        // (field initializer).
        private Event _event;

        private Event KeyEvent => _event ??= new Event { type = EventType.KeyDown };

        private Transform _home;
        private RectTransform _panelSpare;
        private string _keyAreaPath, _templatePath;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        private TMP_InputField _target;
        private Registration _registration;
        private Dictionary<string, KeyboardPage> _pages;
        private int _lostFocusFrames;

        private bool _shift, _capsLock;
        private float _lastShiftTime = -1f;
        private KeyboardKey _repeatKey;
        private float _nextRepeatTime;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Uses this keyboard for <paramref name="field"/> (and turns its soft keyboard off).
        /// <paramref name="dock"/> overrides where the keyboard appears for this field.
        /// </summary>
        public void Register(TMP_InputField field, KeyboardLayoutKind kind, RectTransform dock = null)
        {
            if (field == null) return;
            field.shouldHideSoftKeyboard = true;
            field.shouldHideMobileInput = true;
            _fields[field] = new Registration { Kind = kind, Dock = dock };
        }

        public void Unregister(TMP_InputField field)
        {
            if (field == null) return;
            _fields.Remove(field);
            // May be called from a field's OnDisable / OnDestroy: hide only, LateUpdate
            // moves the panel home.
            if (field == _target) Detach(returnHome: false);
        }

        /// <summary>Closes the keyboard by deselecting the field being typed into.</summary>
        public void Close()
        {
            if (_target == null) return;
            var field = _target;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == field.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            field.DeactivateInputField();
            Detach();
        }

        // ── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            _home = transform;
            _keyAreaPath = PathFrom(panel, keyArea);
            _templatePath = PathFrom(panel, keyTemplate.transform);
            keyTemplate.gameObject.SetActive(false);

            // While attached, the panel is a child of the field: destroying the field (or its
            // page) destroys the panel too. Keep an inactive spare to rebuild it from.
            _panelSpare = Instantiate(panel, _home);
            _panelSpare.name = panel.name + " (Spare)";
            _panelSpare.gameObject.SetActive(false);

            SetupPanel();
        }

        private void SetupPanel()
        {
            var go = panel.gameObject;
            if (!go.TryGetComponent<KeyboardSurface>(out _)) go.AddComponent<KeyboardSurface>();

            // Never resized or positioned by a layout group of the field it is attached to.
            if (!go.TryGetComponent<LayoutElement>(out var layout)) layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            if (!go.TryGetComponent(out _canvas)) _canvas = go.AddComponent<Canvas>();
            if (!go.TryGetComponent(out _raycaster)) _raycaster = go.AddComponent<GraphicRaycaster>();
            go.SetActive(true);
            SetVisible(false);
        }

        // The panel was destroyed with the field it was attached to: rebuild it from the spare.
        private bool EnsurePanel()
        {
            if (panel != null) return true;
            if (_panelSpare == null) return false;

            panel = Instantiate(_panelSpare, _home);
            panel.name = _panelSpare.name.Replace(" (Spare)", string.Empty);
            keyArea = (RectTransform)panel.Find(_keyAreaPath);
            keyTemplate = panel.Find(_templatePath).GetComponent<KeyboardKey>();
            _keys.Clear();
            _pages = null;
            _registration = null;
            SetupPanel();
            return true;
        }

        private static string PathFrom(Transform root, Transform child)
        {
            var path = child.name;
            for (var t = child.parent; t != null && t != root; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        // Only hide here: OnDisable also runs while the hierarchy is being deactivated (e.g.
        // leaving Play mode), when Unity refuses to reparent the panel out of the field it
        // is attached to. LateUpdate moves it home once that is allowed again.
        private void OnDisable() => Detach(returnHome: false);

        // Destroyed while attached: don't leave the panel behind under the field.
        private void OnDestroy()
        {
            if (panel != null && panel.parent != _home) Destroy(panel.gameObject);
        }

        private void LateUpdate()
        {
            if (_target == null) ReturnPanelHome();

            var focused = FindFocusedField();
            if (focused != null)
            {
                _lostFocusFrames = 0;
                if (focused != _target) Attach(focused);
            }
            else if (_target != null && ++_lostFocusFrames > 1)
            {
                // A frame of grace: TMP activates a field in the LateUpdate after it is
                // selected, so focus can read false for one frame while switching fields.
                Detach();
            }

            // Only into the field that still has focus (not during the grace frame above).
            if (_repeatKey != null && _target != null && _target.isFocused &&
                Time.unscaledTime >= _nextRepeatTime)
            {
                // From now, not from the last due time: after a frame stall, catching up
                // would delete a burst of characters on consecutive frames.
                _nextRepeatTime = Time.unscaledTime + repeatInterval;
                Backspace();
                KeyPressed?.Invoke();   // each repeat, like the first press
            }
        }

        private readonly List<TMP_InputField> _destroyedFields = new();

        private TMP_InputField FindFocusedField()
        {
            TMP_InputField focused = null;
            foreach (var field in _fields.Keys)
            {
                if (field == null) _destroyedFields.Add(field);   // destroyed without Unregister
                else if (focused == null && field.isActiveAndEnabled && field.isFocused) focused = field;
            }

            if (_destroyedFields.Count > 0)
            {
                foreach (var field in _destroyedFields) _fields.Remove(field);
                _destroyedFields.Clear();
            }
            return focused;
        }

        // ── Show / hide ───────────────────────────────────────────────

        private void Attach(TMP_InputField field)
        {
            if (!EnsurePanel()) return;
            var registration = _fields[field];
            bool sameLayout = _registration != null && _registration.Kind == registration.Kind;

            _target = field;
            _registration = registration;

            // Under the field, so key presses keep its selection (see class summary).
            var dock = registration.Dock != null ? registration.Dock : defaultDock;
            panel.SetParent(field.transform, worldPositionStays: false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.SetAsLastSibling();
            if (dock != null)
            {
                panel.position = dock.position;
                panel.rotation = dock.rotation;
                float parentScale = field.transform.lossyScale.x;
                if (Mathf.Abs(parentScale) > 1e-6f)
                    panel.localScale = Vector3.one * (dock.lossyScale.x / parentScale);
            }
            else
            {
                // No dock anywhere: just below the field, in its own space — never the
                // offset or scale left over from the previous field.
                var fieldRect = ((RectTransform)field.transform).rect;
                panel.localRotation = Quaternion.identity;
                panel.localScale = Vector3.one;
                panel.localPosition = new Vector3(
                    fieldRect.center.x, fieldRect.yMin - spacing - panel.rect.height / 2f, 0f);
            }

            var parentCanvas = field.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                _canvas.additionalShaderChannels |= parentCanvas.rootCanvas.additionalShaderChannels;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;

            _shift = _capsLock = false;   // every field starts lower case
            if (!sameLayout || _pages == null)
            {
                _pages = KeyboardLayouts.PagesFor(registration.Kind, out var start);
                ShowPage(start);
            }
            else
            {
                // Focus moved straight to another field with the same layout: keep the page.
                RefreshLabels();
            }

            SetVisible(true);
        }

        private void Detach(bool returnHome = true)
        {
            _repeatKey = null;
            _target = null;
            _registration = null;   // the next focus starts on the layout's first page
            SetVisible(false);
            if (returnHome) ReturnPanelHome();
        }

        // Back under the controller, so the panel is never left under a field that may be
        // hidden or destroyed. Call only outside (de)activation callbacks (see OnDisable).
        private void ReturnPanelHome()
        {
            if (panel == null || _home == null || panel.parent == _home) return;
            panel.SetParent(_home, worldPositionStays: false);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
            if (_raycaster != null) _raycaster.enabled = visible;
        }

        // ── Layout ────────────────────────────────────────────────────

        private void ShowPage(string name)
        {
            if (_pages == null || !_pages.TryGetValue(name, out var page)) return;
            _repeatKey = null;

            var area = keyArea.rect;
            int rows = page.Rows.Count;
            float unitH = (area.height - (rows - 1) * spacing) / rows;

            // One unit width for the whole page, so keys line up across rows.
            float unitW = unitH * page.MaxUnitAspect;
            foreach (var row in page.Rows)
            {
                float units = 0f;
                foreach (var key in row) units += key.Width;
                float fit = (area.width - (row.Length - 1) * spacing) / units;
                unitW = Mathf.Min(unitW, fit);
            }

            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                var row = page.Rows[r];
                float rowWidth = (row.Length - 1) * spacing;
                foreach (var key in row) rowWidth += key.Width * unitW;

                float x = area.center.x - rowWidth / 2f;
                float y = area.yMax - r * (unitH + spacing) - unitH / 2f;
                foreach (var def in row)
                {
                    float w = def.Width * unitW;
                    var key = GetKey(index++);
                    key.Rect.anchorMin = key.Rect.anchorMax = key.Rect.pivot = new Vector2(0.5f, 0.5f);
                    key.Rect.sizeDelta = new Vector2(w, unitH);
                    key.Rect.localPosition = new Vector3(x + w / 2f, y, 0f);
                    key.Setup(def, LabelFor(def), IconFor(def));
                    x += w + spacing;
                }
            }

            for (int i = index; i < _keys.Count; i++)
                _keys[i].gameObject.SetActive(false);

            RefreshLabels();
        }

        private KeyboardKey GetKey(int index)
        {
            if (index < _keys.Count)
            {
                _keys[index].gameObject.SetActive(true);
                return _keys[index];
            }

            var key = Instantiate(keyTemplate, keyArea);
            key.name = $"Key {index}";
            key.gameObject.SetActive(true);
            key.Pressed += OnKeyPressed;
            key.Released += OnKeyReleased;
            key.Clicked += OnKeyClicked;
            _keys.Add(key);
            return key;
        }

        private string LabelFor(KeyDef def)
        {
            if (def.Label != null) return def.Label;
            switch (def.Action)
            {
                case KeyAction.Backspace: return backspaceLabel;
                case KeyAction.Shift:     return shiftLabel;
                case KeyAction.Done:      return doneLabel;
            }
            if (def.Value == " ") return spaceLabel;
            bool upper = def.IsLetter && (_shift || _capsLock);
            return upper ? def.Value.ToUpperInvariant() : def.Value;
        }

        private Sprite IconFor(KeyDef def) => def.Action switch
        {
            KeyAction.Backspace => backspaceIcon,
            KeyAction.Shift     => shiftIcon,
            KeyAction.Done      => doneIcon,
            _                   => null,
        };

        // Letters follow Shift; Shift is lit while on.
        private void RefreshLabels()
        {
            foreach (var key in _keys)
            {
                if (!key.gameObject.activeSelf) continue;
                var def = key.Def;
                if (def.IsLetter) key.SetLabel(LabelFor(def));
                if (def.Action == KeyAction.Shift) key.SetActive(_shift || _capsLock);
            }
        }

        // ── Keys ──────────────────────────────────────────────────────

        private void OnKeyPressed(KeyboardKey key)
        {
            // Backspace acts on press and repeats while held; other keys act on click.
            if (key.Def.Action != KeyAction.Backspace || _target == null) return;
            Backspace();
            _repeatKey = key;
            _nextRepeatTime = Time.unscaledTime + repeatDelay;
            KeyPressed?.Invoke();
        }

        private void OnKeyReleased(KeyboardKey key)
        {
            if (_repeatKey == key) _repeatKey = null;
        }

        private void OnKeyClicked(KeyboardKey key)
        {
            if (_target == null) return;
            var def = key.Def;

            switch (def.Action)
            {
                case KeyAction.Insert:
                    bool upper = _shift || _capsLock;
                    Type(def.IsLetter && upper ? def.Value.ToUpperInvariant() : def.Value);
                    if (_shift && !_capsLock && def.IsLetter)
                    {
                        _shift = false;   // one-shot Shift
                        RefreshLabels();
                    }
                    break;

                case KeyAction.Shift:
                    float now = Time.unscaledTime;
                    if (_capsLock) _shift = _capsLock = false;
                    else if (_shift && now - _lastShiftTime <= capsLockDoubleTap) _capsLock = true;
                    else _shift = !_shift;
                    _lastShiftTime = now;
                    RefreshLabels();
                    break;

                case KeyAction.Page:
                    _shift = _capsLock = false;
                    ShowPage(def.Value);
                    break;

                case KeyAction.Done:
                    var field = _target;
                    Close();
                    DonePressed?.Invoke(field);
                    break;

                case KeyAction.Backspace:
                    return;   // handled on press
            }
            KeyPressed?.Invoke();
        }

        private void Type(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var e = KeyEvent;
            e.keyCode = KeyCode.None;
            e.modifiers = EventModifiers.None;
            var field = _target;
            foreach (char c in text)
            {
                // An onValueChanged listener may close the keyboard mid-string (".com").
                if (field == null || field != _target) return;
                e.character = c;
                field.ProcessEvent(e);
            }
            // Once per string: inserting doesn't read the mesh's character positions.
            if (field != null && field == _target) RefreshTarget();
        }

        private void Backspace()
        {
            if (_target == null) return;
            var e = KeyEvent;
            e.keyCode = KeyCode.Backspace;
            e.modifiers = EventModifiers.None;
            e.character = '\0';
            _target.ProcessEvent(e);
            RefreshTarget();
        }

        // What TMP's own key handling (TMP_InputField.OnUpdateSelected) does after every key,
        // which ProcessEvent skips: redraw the label, and rebuild the text mesh at once so
        // its character positions are current. Stale positions would place the caret and
        // the horizontal scroll from the text before the edit (deleting shifted the text),
        // and Backspace reads them to find what to delete.
        private void RefreshTarget()
        {
            _target.ForceLabelUpdate();
            if (_target.textComponent != null)
                _target.textComponent.ForceMeshUpdate();
        }
    }
}
