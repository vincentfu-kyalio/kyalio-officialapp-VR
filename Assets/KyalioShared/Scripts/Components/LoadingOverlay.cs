using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Global loading overlay. Call Show/Hide to toggle visibility.
    /// Attach to the LoadingOverlay GameObject under the Canvas.
    /// </summary>
    public class LoadingOverlay : MonoBehaviour
    {
        public static LoadingOverlay Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
