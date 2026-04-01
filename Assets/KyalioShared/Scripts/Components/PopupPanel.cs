using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Visual popup panel. Sits under the Canvas as a UI parent object.
    /// Controlled by PopupManager — do not call Show/Hide directly from pages.
    ///
    /// Inspector: assign messageText, then each button group with its buttons.
    /// All three groups (yesNoGroup, deleteCancelGroup, doneGroup) must be assigned.
    /// </summary>
    public class PopupPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Yes / No")]
        [SerializeField] private GameObject yesNoGroup;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Delete / Cancel")]
        [SerializeField] private GameObject deleteCancelGroup;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button cancelButton;

        [Header("Done")]
        [SerializeField] private GameObject doneGroup;
        [SerializeField] private Button doneButton;

        private Action _primaryAction;
        private Action _secondaryAction;

        private void Awake()
        {
            yesButton.onClick.AddListener(() => { _primaryAction?.Invoke(); Hide(); });
            noButton.onClick.AddListener(() => { _secondaryAction?.Invoke(); Hide(); });
            deleteButton.onClick.AddListener(() => { _primaryAction?.Invoke(); Hide(); });
            cancelButton.onClick.AddListener(() => { _secondaryAction?.Invoke(); Hide(); });
            doneButton.onClick.AddListener(() => { _primaryAction?.Invoke(); Hide(); });

            gameObject.SetActive(false);
        }

        public void ShowYesNo(string message, Action onYes, Action onNo = null)
            => Show(message, yesNoGroup, onYes, onNo);

        public void ShowDeleteCancel(string message, Action onDelete, Action onCancel = null)
            => Show(message, deleteCancelGroup, onDelete, onCancel);

        public void ShowDone(string message, Action onDone = null)
            => Show(message, doneGroup, onDone, null);

        public void Hide() => gameObject.SetActive(false);

        private void Show(string message, GameObject activeGroup, Action primary, Action secondary)
        {
            messageText.text = message;
            _primaryAction = primary;
            _secondaryAction = secondary;

            yesNoGroup.SetActive(yesNoGroup == activeGroup);
            deleteCancelGroup.SetActive(deleteCancelGroup == activeGroup);
            doneGroup.SetActive(doneGroup == activeGroup);

            gameObject.SetActive(true);
        }
    }
}
