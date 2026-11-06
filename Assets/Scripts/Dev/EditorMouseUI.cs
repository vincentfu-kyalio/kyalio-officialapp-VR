#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Editor-only: drive the VR UI with the mouse in the Game view when no headset is connected.
///
/// On Play, if no XR device is active (no Quest Link), this:
///   - disables the Interaction SDK PointableCanvasModule (exclusive mode turns every other
///     input module off) and enables InputSystemUIInputModule for mouse input,
///   - adds a GraphicRaycaster to every world-space Canvas,
///   - adds a camera framing the main world-space Canvas, since the headset camera is not
///     tracked without a device, and uses it as the canvases' event camera.
/// With a headset connected, nothing changes.
///
/// Toggle with Kyalio > Editor Mouse UI (saved per machine). Compiled out of player builds.
/// </summary>
public static class EditorMouseUI
{
    private const string MenuPath = "Kyalio/Editor Mouse UI";
    private const string PrefKey = "Kyalio.EditorMouseUI";

    private static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

    [MenuItem(MenuPath)]
    private static void Toggle() => EditorPrefs.SetBool(PrefKey, !Enabled);

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnPlay()
    {
        if (Enabled)
            new GameObject("[EditorMouseUI]").AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Let PointableCanvasModule.Start (which disables the other modules) run first.
            yield return null;

            if (XRSettings.isDeviceActive)
            {
                Debug.Log("[EditorMouseUI] Headset active — keeping VR input.");
                Destroy(gameObject);
                yield break;
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.renderMode == RenderMode.WorldSpace)
                .ToArray();
            if (canvases.Length == 0)
            {
                Debug.LogWarning("[EditorMouseUI] No world-space Canvas found.");
                yield break;
            }

            UseMouseInputModule();

            var camera = CreateCameraFacing(LargestRootCanvas(canvases));
            foreach (var canvas in canvases)
            {
                canvas.worldCamera = camera;
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            Debug.Log("[EditorMouseUI] No headset — mouse input enabled in the Game view " +
                      "(Kyalio > Editor Mouse UI to turn off).");
        }

        private static void UseMouseInputModule()
        {
            var eventSystem = EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning("[EditorMouseUI] No EventSystem in the scene.");
                return;
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                module.enabled = module is InputSystemUIInputModule;
        }

        private static Canvas LargestRootCanvas(Canvas[] canvases) =>
            canvases.Where(c => c.isRootCanvas)
                .OrderByDescending(c => ((RectTransform)c.transform).rect.width * c.transform.lossyScale.x)
                .FirstOrDefault() ?? canvases[0];

        private static Camera CreateCameraFacing(Canvas canvas)
        {
            var rect = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) * 0.5f;
            var height = Vector3.Distance(corners[0], corners[1]);
            var width = Vector3.Distance(corners[1], corners[2]);

            var camera = new GameObject("[EditorMouseUI] Camera").AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.fieldOfView = 60f;
            // Render after the untracked headset camera so this view fills the Game view.
            camera.depth = 100f;

            // Back off until the whole canvas fits, with a small margin.
            var vertical = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var horizontal = Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView, camera.aspect) * 0.5f * Mathf.Deg2Rad;
            var distance = Mathf.Max(height * 0.5f / Mathf.Tan(vertical), width * 0.5f / Mathf.Tan(horizontal)) * 1.05f;

            camera.transform.SetPositionAndRotation(
                center - rect.forward * distance,
                Quaternion.LookRotation(rect.forward, rect.up));
            return camera;
        }
    }
}
#endif
