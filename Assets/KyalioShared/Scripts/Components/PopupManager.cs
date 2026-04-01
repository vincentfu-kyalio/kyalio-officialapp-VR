using System;
using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Global popup manager. Lives on its own empty GameObject (not under the Canvas).
    /// Inspector: assign the PopupPanel reference (the UI parent under the Canvas).
    ///
    /// Usage:
    ///   PopupManager.Instance.ShowYesNo("Are you sure?", onYes: () => DoSomething());
    ///   PopupManager.Instance.ShowDeleteCancel("Delete this item?", onDelete: () => Delete());
    ///   PopupManager.Instance.ShowDone("Changes saved.");
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        public static PopupManager Instance { get; private set; }

        [SerializeField] private PopupPanel popupPanel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ShowYesNo(string message, Action onYes, Action onNo = null)
            => popupPanel.ShowYesNo(message, onYes, onNo);

        public void ShowDeleteCancel(string message, Action onDelete, Action onCancel = null)
            => popupPanel.ShowDeleteCancel(message, onDelete, onCancel);

        public void ShowDone(string message, Action onDone = null)
            => popupPanel.ShowDone(message, onDone);

        public void Hide() => popupPanel.Hide();
    }
}
