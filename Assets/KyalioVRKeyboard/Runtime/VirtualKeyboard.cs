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
    /// parent mask clips it). A press on a key (not a Selectable) makes the event system
    /// look up the hierarchy for a select handler, find the already-selected field and leave
    /// the selection alone. Typing goes through TMP_InputField.ProcessEvent, so content type,
    /// validation, character limit and selection replacement all apply.
    ///
    /// Scene setup (see README): this component on an object that stays active (it polls
    /// focus every frame), with a child panel (a RectTransform with the background Image,
    /// sized as the keyboard) holding the key area and inactive key templates with
    /// <see cref="KeyboardKey"/>. Only the panel moves under the focused field; it fades and
    /// slides in and out, and hides by disabling its own Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class VirtualKeyboard : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The visible keyboard: background Image, sized as the keyboard. A child of this object.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("Where the keys are laid out (inside the panel, inset for padding).")]
        [SerializeField] private RectTransform keyArea;
        [Tooltip("Template for character keys (letters, digits, symbols, space). Required; kept inactive.")]
        [SerializeField] private KeyboardKey keyTemplate;
        [Tooltip("Optional template for Shift, Backspace and page keys (123 / ABC / #+=). " +
                 "Falls back to the character template.")]
        [SerializeField] private KeyboardKey functionKeyTemplate;
        [Tooltip("Optional template for the primary action (Done). Falls back to the function " +
                 "template, then the character template.")]
        [SerializeField] private KeyboardKey accentKeyTemplate;
        [Tooltip("Where the keyboard appears (its center) unless a field registers its own dock.")]
        [SerializeField] private RectTransform defaultDock;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float spacing = 12f;
        [Tooltip("Sorting order of the keyboard's own Canvas, so it draws above the page.")]
        [SerializeField] private int sortingOrder = 100;

        [Header("Labels")]
        [SerializeField] private string backspaceLabel = "Del";
        [SerializeField] private string shiftLabel = "Shift";
        [SerializeField] private string capsLockLabel = "CAPS";
        [SerializeField] private string doneLabel = "Done";
        [SerializeField] private string spaceLabel = "Space";

        [Header("Icons (optional; replace the label when set)")]
        [SerializeField] private Sprite backspaceIcon;
        [SerializeField] private Sprite shiftIcon;
        [Tooltip("Shift key while caps lock is on. Falls back to the Shift icon.")]
        [SerializeField] private Sprite capsLockIcon;
        [SerializeField] private Sprite doneIcon;

        [Header("Show / hide")]
        [SerializeField, Min(0f)] private float showDuration = 0.15f;
        [SerializeField, Min(0f)] private float hideDuration = 0.1f;
        [Tooltip("How far (in panel units) the keyboard slides up while appearing, and down while hiding.")]
        [SerializeField, Min(0f)] private float slideDistance = 30f;

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
            // The field's own settings, restored by Unregister.
            public bool HideSoftKeyboard, HideMobileInput;
        }

        private enum Fade { None, Showing, Hiding }

        private readonly Dictionary<TMP_InputField, Registration> _fields = new();
        private readonly List<TMP_InputField> _destroyedFields = new();

        // Key objects per template, reused across pages; _shownKeys are the current page's.
        private readonly Dictionary<KeyStyle, List<KeyboardKey>> _pools = new();
        private readonly List<KeyboardKey> _shownKeys = new();

        // Created on first use: Event's setters may not run in a MonoBehaviour constructor
        // (field initializer).
        private Event _event;
        private Event KeyEvent => _event ??= new Event { type = EventType.KeyDown };

        private Transform _home;
        private RectTransform _panelSpare;
        private string _keyAreaPath;
        private readonly Dictionary<KeyStyle, string> _templatePaths = new();
        private bool _hasFunctionTemplate, _hasAccentTemplate;
        private Canvas _canvas;
        private BaseRaycaster _raycaster;
        private CanvasGroup _group;

        private TMP_InputField _target;
        private Registration _registration;
        private Dictionary<string, KeyboardPage> _pages;
        private int _lostFocusFrames;

        private bool _shift, _capsLock;
        private float _lastShiftTime = -1f;
        private KeyboardKey _repeatKey;
        private float _nextRepeatTime;

        private Fade _fade;
        private float _fadeFrom, _fadeStart;
        private Vector3 _restPosition;   // the panel's placed localPosition while attached

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Uses this keyboard for <paramref name="field"/> (and turns its soft keyboard off).
        /// <paramref name="dock"/> overrides where the keyboard appears for this field.
        /// </summary>
        /// <returns>
        /// False, changing nothing, when this keyboard cannot serve the field: not set up
        /// correctly (<see cref="IsUsable"/>), or its controller is disabled / inactive (it
        /// would never notice the field gaining focus). The field then keeps its platform
        /// keyboard.
        /// </returns>
        public bool Register(TMP_InputField field, KeyboardLayoutKind kind, RectTransform dock = null)
        {
            // Not isActiveAndEnabled: that stays false until OnEnable has run, and callers
            // often register from an Awake that runs before it.
            if (field == null || !IsUsable || !enabled || !gameObject.activeInHierarchy) return false;

            if (!_fields.TryGetValue(field, out var registration))
                registration = new Registration
                {
                    HideSoftKeyboard = field.shouldHideSoftKeyboard,
                    HideMobileInput = field.shouldHideMobileInput,
                };
            registration.Kind = kind;
            registration.Dock = dock;
            _fields[field] = registration;

            field.shouldHideSoftKeyboard = true;
            field.shouldHideMobileInput = true;
            return true;
        }

        /// <summary>Stops serving the field and gives it back its platform keyboard settings.</summary>
        public void Unregister(TMP_InputField field)
        {
            if (field == null || !_fields.TryGetValue(field, out var registration)) return;
            _fields.Remove(field);
            field.shouldHideSoftKeyboard = registration.HideSoftKeyboard;
            field.shouldHideMobileInput = registration.HideMobileInput;
            // May be called from a field's OnDisable / OnDestroy: hide at once, no fade;
            // LateUpdate moves the panel home.
            if (field == _target) Detach(animate: false);
        }

        /// <summary>Closes the keyboard by deselecting the field being typed into.</summary>
        public void Close()
        {
            if (_target == null) return;
            var field = _target;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == field.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            field.DeactivateInputField();
            Detach(animate: true);
        }

        /// <summary>
        /// The parts the keyboard needs are assigned and laid out as expected (key area inside
        /// the panel, templates inside the key area). When false, Register refuses fields so
        /// they keep their platform keyboard.
        /// </summary>
        public bool IsUsable =>
            panel != null && keyArea != null && keyTemplate != null &&
            IsWithin(keyArea, panel) && IsWithin(keyTemplate.transform, keyArea) &&
            (functionKeyTemplate == null || IsWithin(functionKeyTemplate.transform, keyArea)) &&
            (accentKeyTemplate == null || IsWithin(accentKeyTemplate.transform, keyArea));

        // ── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (!IsUsable)
            {
                Debug.LogError("[VirtualKeyboard] Panel, Key Area and Key Template must be assigned, " +
                               "with the key area inside the panel and the templates inside the key area. " +
                               "Keyboard disabled.", this);
                enabled = false;
                return;
            }

            _home = transform;
            _hasFunctionTemplate = functionKeyTemplate != null;
            _hasAccentTemplate = accentKeyTemplate != null;
            _keyAreaPath = PathFrom(panel, keyArea);
            foreach (KeyStyle style in Enum.GetValues(typeof(KeyStyle)))
            {
                var template = TemplateFor(style);
                _templatePaths[style] = PathFrom(panel, template.transform);
                template.gameObject.SetActive(false);
            }

            // While attached, the panel is a child of the field: destroying the field (or its
            // page) destroys the panel too. Keep an inactive spare to rebuild it from.
            _panelSpare = Instantiate(panel, _home);
            _panelSpare.name = panel.name + " (Spare)";
            _panelSpare.gameObject.SetActive(false);

            SetupPanel();
        }

        // Only hide here: OnDisable also runs while the hierarchy is being deactivated (e.g.
        // leaving Play mode), when Unity refuses to reparent the panel out of the field it
        // is attached to. LateUpdate moves it home once that is allowed again.
        private void OnDisable() => Detach(animate: false);

        // Destroyed while attached: don't leave the panel behind under the field.
        private void OnDestroy()
        {
            if (_home != null && panel != null && panel.parent != _home) Destroy(panel.gameObject);
        }

        private void LateUpdate()
        {
            if (_target == null && _fade != Fade.Hiding) ReturnPanelHome();

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
                Detach(animate: true);
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

            UpdateFade();
        }

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

        // ── Panel ─────────────────────────────────────────────────────

        private void SetupPanel()
        {
            var go = panel.gameObject;
            if (!go.TryGetComponent<KeyboardSurface>(out _)) go.AddComponent<KeyboardSurface>();

            // Never resized or positioned by a layout group of the field it is attached to.
            if (!go.TryGetComponent<LayoutElement>(out var layout)) layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            if (!go.TryGetComponent(out _canvas)) _canvas = go.AddComponent<Canvas>();
            // Keep a raycaster set up on the panel (e.g. an XR one); otherwise a plain one.
            if (!go.TryGetComponent(out _raycaster)) _raycaster = go.AddComponent<GraphicRaycaster>();
            if (!go.TryGetComponent(out _group)) _group = go.AddComponent<CanvasGroup>();

            go.SetActive(true);
            _fade = Fade.None;
            SetVisible(false);
        }

        // The panel was destroyed with the field it was attached to: rebuild it from the spare.
        private bool EnsurePanel()
        {
            if (panel != null) return true;
            if (_panelSpare == null) return false;

            panel = Instantiate(_panelSpare, _home);
            panel.name = _panelSpare.name.Replace(" (Spare)", string.Empty);
            keyArea = (RectTransform)FindUnder(panel, _keyAreaPath);
            keyTemplate = TemplateAt(KeyStyle.Character);
            functionKeyTemplate = _hasFunctionTemplate ? TemplateAt(KeyStyle.Function) : null;
            accentKeyTemplate = _hasAccentTemplate ? TemplateAt(KeyStyle.Accent) : null;
            _pools.Clear();
            _shownKeys.Clear();
            _pages = null;
            _registration = null;
            SetupPanel();
            return true;
        }

        private KeyboardKey TemplateAt(KeyStyle style) =>
            FindUnder(panel, _templatePaths[style]).GetComponent<KeyboardKey>();

        private KeyboardKey TemplateFor(KeyStyle style) => style switch
        {
            KeyStyle.Accent   => accentKeyTemplate != null ? accentKeyTemplate : TemplateFor(KeyStyle.Function),
            KeyStyle.Function => functionKeyTemplate != null ? functionKeyTemplate : keyTemplate,
            _                 => keyTemplate,
        };

        private static bool IsWithin(Transform child, Transform root)
        {
            for (var t = child; t != null; t = t.parent)
                if (t == root) return true;
            return false;
        }

        // Path of child below root ("" when child is root itself). IsUsable guarantees
        // child is root or one of its descendants.
        private static string PathFrom(Transform root, Transform child)
        {
            if (child == root) return string.Empty;
            var path = child.name;
            for (var t = child.parent; t != null && t != root; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        private static Transform FindUnder(Transform root, string path) =>
            string.IsNullOrEmpty(path) ? root : root.Find(path);

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
                // No dock anywhere: just below the field, in its own space, never the
                // offset or scale left over from the previous field.
                var fieldRect = ((RectTransform)field.transform).rect;
                panel.localRotation = Quaternion.identity;
                panel.localScale = Vector3.one;
                panel.localPosition = new Vector3(
                    fieldRect.center.x, fieldRect.yMin - spacing - panel.rect.height / 2f, 0f);
            }
            _restPosition = panel.localPosition;

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

            // Fade in, from where a hide in progress left off.
            bool wasShown = _canvas.enabled;
            SetVisible(true);
            StartFade(Fade.Showing, wasShown ? _group.alpha : 0f);
        }

        // animate: false where reparenting or waiting is not possible (OnDisable, Unregister).
        private void Detach(bool animate)
        {
            _repeatKey = null;
            _target = null;
            _registration = null;   // the next focus starts on the layout's first page

            if (_raycaster != null) _raycaster.enabled = false;   // no clicks while fading out
            if (animate && hideDuration > 0f && _canvas != null && _canvas.enabled && panel != null)
            {
                StartFade(Fade.Hiding, _group.alpha);   // LateUpdate moves it home when done
                return;
            }

            _fade = Fade.None;
            SetVisible(false);
            if (animate) ReturnPanelHome();
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
            if (_group != null) _group.alpha = visible ? _group.alpha : 0f;
        }

        // ── Fade ──────────────────────────────────────────────────────

        private void StartFade(Fade fade, float fromAlpha)
        {
            _fade = fade;
            _fadeFrom = fromAlpha;
            _fadeStart = Time.unscaledTime;
            UpdateFade();
        }

        // Fades the panel's alpha and slides it: up into place while showing, down while
        // hiding. Driven by the controller, so it runs even while the panel's page is hidden.
        private void UpdateFade()
        {
            if (_fade == Fade.None || panel == null || _group == null) return;

            bool showing = _fade == Fade.Showing;
            float target = showing ? 1f : 0f;
            float duration = (showing ? showDuration : hideDuration) * Mathf.Abs(target - _fadeFrom);
            float t = duration > 0f ? Mathf.Clamp01((Time.unscaledTime - _fadeStart) / duration) : 1f;
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);   // ease out cubic

            float alpha = Mathf.Lerp(_fadeFrom, target, eased);
            _group.alpha = alpha;
            // Offset by how far from fully shown: 0 at alpha 1, slideDistance at alpha 0.
            panel.localPosition = _restPosition + panel.localRotation * Vector3.down *
                                  (slideDistance * panel.localScale.y * (1f - alpha));

            if (t < 1f) return;
            if (showing)
            {
                _fade = Fade.None;
                return;
            }
            _fade = Fade.None;
            SetVisible(false);
            ReturnPanelHome();
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

            _shownKeys.Clear();
            var used = new Dictionary<KeyStyle, int>();
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
                    used.TryGetValue(def.Style, out int n);
                    used[def.Style] = n + 1;

                    var key = GetKey(def.Style, n);
                    key.Rect.anchorMin = key.Rect.anchorMax = key.Rect.pivot = new Vector2(0.5f, 0.5f);
                    key.Rect.sizeDelta = new Vector2(w, unitH);
                    key.Rect.localPosition = new Vector3(x + w / 2f, y, 0f);
                    key.Setup(def, LabelFor(def), IconFor(def));
                    _shownKeys.Add(key);
                    x += w + spacing;
                }
            }

            // Hide the pooled keys this page doesn't use.
            foreach (var pool in _pools)
            {
                used.TryGetValue(pool.Key, out int n);
                for (int i = n; i < pool.Value.Count; i++)
                    pool.Value[i].gameObject.SetActive(false);
            }

            RefreshLabels();
        }

        private KeyboardKey GetKey(KeyStyle style, int index)
        {
            if (!_pools.TryGetValue(style, out var pool)) _pools[style] = pool = new List<KeyboardKey>();
            if (index < pool.Count)
            {
                pool[index].gameObject.SetActive(true);
                return pool[index];
            }

            var key = Instantiate(TemplateFor(style), keyArea);
            key.name = $"{style} Key {index}";
            key.gameObject.SetActive(true);
            key.Pressed += OnKeyPressed;
            key.Released += OnKeyReleased;
            key.Clicked += OnKeyClicked;
            pool.Add(key);
            return key;
        }

        private string LabelFor(KeyDef def)
        {
            if (def.Label != null) return def.Label;
            switch (def.Action)
            {
                case KeyAction.Backspace: return backspaceLabel;
                case KeyAction.Shift:     return _capsLock ? capsLockLabel : shiftLabel;
                case KeyAction.Done:      return doneLabel;
            }
            if (def.Value == " ") return spaceLabel;
            bool upper = def.IsLetter && (_shift || _capsLock);
            return upper ? def.Value.ToUpperInvariant() : def.Value;
        }

        private Sprite IconFor(KeyDef def) => def.Action switch
        {
            KeyAction.Backspace => backspaceIcon,
            KeyAction.Shift     => _capsLock && capsLockIcon != null ? capsLockIcon : shiftIcon,
            KeyAction.Done      => doneIcon,
            _                   => null,
        };

        // Letters follow Shift; Shift shows on / caps lock.
        private void RefreshLabels()
        {
            foreach (var key in _shownKeys)
            {
                var def = key.Def;
                if (def.IsLetter) key.SetLabel(LabelFor(def));
                if (def.Action == KeyAction.Shift)
                {
                    key.SetLabel(LabelFor(def), IconFor(def));
                    key.SetToggle(_shift, _capsLock);
                }
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
            var field = _target;
            field.ProcessEvent(e);
            // An onValueChanged listener may have closed the keyboard (see Type).
            if (field == _target) RefreshTarget();
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
