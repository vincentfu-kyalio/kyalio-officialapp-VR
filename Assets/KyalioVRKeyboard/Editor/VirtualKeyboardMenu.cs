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
    ///   VR Keyboard        VirtualKeyboard (controller, stays here)
    ///   └ Panel            background Image — the visible keyboard, moved under the focused field
    ///     └ Keys           key area (inset for padding)
    ///       └ Key Template KeyboardKey: background Image + Label (TMP) + Icon (inactive)
    ///   Keyboard Dock      where the keyboard appears (sibling; move it in the Editor)
    ///
    /// Restyle the template and panel freely; every key is cloned from the template.
    /// </summary>
    internal static class VirtualKeyboardMenu
    {
        private static readonly Vector2 PanelSize = new(1400f, 500f);
        private const float Padding = 24f;

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

            // Key template
            var template = NewRect("Key Template", keys);
            template.sizeDelta = new Vector2(100f, 100f);
            var keyImage = template.gameObject.AddComponent<Image>();
            keyImage.sprite = rounded;
            keyImage.type = Image.Type.Sliced;
            keyImage.color = new Color(0.26f, 0.27f, 0.30f, 1f);

            var label = NewRect("Label", template);
            Stretch(label);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "A";
            text.fontSize = 40f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            var iconRect = NewRect("Icon", template);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            iconRect.gameObject.SetActive(false);

            var key = template.gameObject.AddComponent<KeyboardKey>();
            var keySo = new SerializedObject(key);
            keySo.FindProperty("background").objectReferenceValue = keyImage;
            keySo.FindProperty("label").objectReferenceValue = text;
            keySo.FindProperty("icon").objectReferenceValue = icon;
            keySo.ApplyModifiedPropertiesWithoutUndo();
            template.gameObject.SetActive(false);

            // Dock: bottom centre of the parent by default
            var dock = NewRect("Keyboard Dock", parent);
            dock.anchorMin = dock.anchorMax = new Vector2(0.5f, 0f);
            dock.anchoredPosition = new Vector2(0f, PanelSize.y / 2f + 40f);
            dock.sizeDelta = PanelSize;   // size only helps placing it in the Scene view

            var so = new SerializedObject(keyboard);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("keyArea").objectReferenceValue = keys;
            so.FindProperty("keyTemplate").objectReferenceValue = key;
            so.FindProperty("defaultDock").objectReferenceValue = dock;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create VR Keyboard");
            Undo.RegisterCreatedObjectUndo(dock.gameObject, "Create VR Keyboard Dock");
            Selection.activeGameObject = root.gameObject;
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
