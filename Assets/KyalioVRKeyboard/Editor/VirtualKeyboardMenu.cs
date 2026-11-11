using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.VRKeyboard.Editor
{
    /// <summary>
    /// GameObject → Kyalio → VR Keyboard: builds a ready-to-use keyboard under the selected
    /// object (select the world-space Canvas, or an object under it that stays active).
    ///
    ///   VR Keyboard               VirtualKeyboard (controller, stays here)
    ///   └ Panel                   background Image: the visible keyboard, moved under the focused field
    ///     └ Keys                  key area (inset for padding)
    ///       ├ Key Template            character keys
    ///       ├ Function Key Template   Shift, Del, 123 / ABC / #+=  (with a caps-lock indicator)
    ///       └ Accent Key Template     Done
    ///   Keyboard Dock             where the keyboard appears (sibling; move it in the Editor)
    ///
    /// Each template: KeyboardKey + background Image + Label (TMP) + Icon (inactive). Restyle
    /// them freely; every key is cloned from the template of its kind.
    /// </summary>
    internal static class VirtualKeyboardMenu
    {
        private static readonly Vector2 PanelSize = new(1400f, 500f);
        private const float Padding = 24f;

        private struct Palette
        {
            public Color Normal, Highlighted, Pressed;
        }

        private static readonly Palette CharacterKeys = new()
        {
            Normal = new Color(0.26f, 0.27f, 0.30f), Highlighted = new Color(0.36f, 0.38f, 0.43f),
            Pressed = new Color(0.18f, 0.19f, 0.21f),
        };
        private static readonly Palette FunctionKeys = new()
        {
            Normal = new Color(0.17f, 0.18f, 0.20f), Highlighted = new Color(0.26f, 0.27f, 0.30f),
            Pressed = new Color(0.11f, 0.12f, 0.13f),
        };
        private static readonly Palette AccentKeys = new()
        {
            Normal = new Color(0.31f, 0.52f, 0.92f), Highlighted = new Color(0.42f, 0.62f, 1f),
            Pressed = new Color(0.22f, 0.40f, 0.75f),
        };

        [MenuItem("GameObject/Kyalio/VR Keyboard", false, 10)]
        private static void Create(MenuCommand command)
        {
            // A GameObject-menu item runs once per selected object; build only one keyboard.
            if (command.context != null && Selection.objects.Length > 1 &&
                command.context != Selection.activeObject)
                return;

            var parent = (command.context as GameObject)?.transform ?? Selection.activeTransform;
            if (parent == null || parent.GetComponentInParent<Canvas>(true) == null)
            {
                EditorUtility.DisplayDialog("VR Keyboard",
                    "Select the world-space Canvas (or an object under it that stays active) first.", "OK");
                return;
            }

            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Controller
            var root = NewRect("VR Keyboard", parent);
            Stretch(root);
            var keyboard = root.gameObject.AddComponent<VirtualKeyboard>();

            // Panel
            var panel = NewRect("Panel", root);
            panel.sizeDelta = PanelSize;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);

            // Key area
            var keys = NewRect("Keys", panel);
            Stretch(keys);
            keys.offsetMin = new Vector2(Padding, Padding);
            keys.offsetMax = new Vector2(-Padding, -Padding);

            var character = NewKeyTemplate("Key Template", keys, rounded, CharacterKeys, withLockIndicator: false);
            var function  = NewKeyTemplate("Function Key Template", keys, rounded, FunctionKeys, withLockIndicator: true);
            var accent    = NewKeyTemplate("Accent Key Template", keys, rounded, AccentKeys, withLockIndicator: false);

            // Dock: bottom centre of the parent by default
            var dock = NewRect("Keyboard Dock", parent);
            dock.anchorMin = dock.anchorMax = new Vector2(0.5f, 0f);
            dock.anchoredPosition = new Vector2(0f, PanelSize.y / 2f + 40f);
            dock.sizeDelta = PanelSize;   // only a guide in the Scene view; the Panel sets the size

            var so = new SerializedObject(keyboard);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("keyArea").objectReferenceValue = keys;
            so.FindProperty("keyTemplate").objectReferenceValue = character;
            so.FindProperty("functionKeyTemplate").objectReferenceValue = function;
            so.FindProperty("accentKeyTemplate").objectReferenceValue = accent;
            so.FindProperty("defaultDock").objectReferenceValue = dock;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create VR Keyboard");
            Undo.RegisterCreatedObjectUndo(dock.gameObject, "Create VR Keyboard Dock");
            Selection.activeGameObject = root.gameObject;
        }

        private static KeyboardKey NewKeyTemplate(string name, Transform parent, Sprite sprite,
            Palette palette, bool withLockIndicator)
        {
            var template = NewRect(name, parent);
            template.sizeDelta = new Vector2(100f, 100f);
            var image = template.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = palette.Normal;

            var labelRect = NewRect("Label", template);
            Stretch(labelRect);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "A";
            label.fontSize = 40f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            var iconRect = NewRect("Icon", template);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            iconRect.gameObject.SetActive(false);

            GameObject lockIndicator = null;
            if (withLockIndicator)
            {
                // A short bar along the bottom edge, shown only while caps lock is on.
                var bar = NewRect("Caps Lock Indicator", template);
                bar.anchorMin = new Vector2(0.3f, 0f);
                bar.anchorMax = new Vector2(0.7f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.anchoredPosition = new Vector2(0f, 10f);
                bar.sizeDelta = new Vector2(0f, 6f);
                var barImage = bar.gameObject.AddComponent<Image>();
                barImage.color = Color.white;
                barImage.raycastTarget = false;
                lockIndicator = bar.gameObject;
                lockIndicator.SetActive(false);
            }

            var key = template.gameObject.AddComponent<KeyboardKey>();
            var so = new SerializedObject(key);
            so.FindProperty("background").objectReferenceValue = image;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("lockIndicator").objectReferenceValue = lockIndicator;
            so.FindProperty("normalColor").colorValue = palette.Normal;
            so.FindProperty("highlightedColor").colorValue = palette.Highlighted;
            so.FindProperty("pressedColor").colorValue = palette.Pressed;
            so.ApplyModifiedPropertiesWithoutUndo();

            template.gameObject.SetActive(false);
            return key;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
