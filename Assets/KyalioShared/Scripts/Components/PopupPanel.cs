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
    /// yesNoGroup, deleteCancelGroup and doneGroup must be assigned; accountGroup (Log out /
    /// Exit / Cancel) is needed only for the Account menu.
    ///
    /// Works whether the GameObject starts active or inactive in the scene.
    /// Showing a popup while another is open dismisses the old one: its secondary
    /// action (No / Cancel) runs, so callers awaiting an answer are always released.
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

        [Header("Account (Log out / Exit / Cancel)")]
        [SerializeField] private GameObject accountGroup;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button accountCancelButton;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float fadeInDuration  = 0.15f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.1f;

        private Action _primaryAction;
        private Action _secondaryAction;
        private Action _tertiaryAction;   // third choice of a three-button group (Account: Exit)
        private GameObject _activeGroup;
        private bool _visible;

        private CanvasGroup _group;
        private Coroutine _fade;

        private CanvasGroup Group
        {
            get
            {
                if (_group == null && !TryGetComponent(out _group))
                    _group = gameObject.AddComponent<CanvasGroup>();
                return _group;
            }
        }

        private void Awake()
        {
            yesButton.onClick.AddListener(OnPrimary);
            noButton.onClick.AddListener(OnSecondary);
            deleteButton.onClick.AddListener(OnPrimary);
            cancelButton.onClick.AddListener(OnSecondary);
            doneButton.onClick.AddListener(OnPrimary);
            if (logoutButton != null)        logoutButton.onClick.AddListener(OnPrimary);
            if (accountCancelButton != null) accountCancelButton.onClick.AddListener(OnSecondary);
            if (exitButton != null)          exitButton.onClick.AddListener(() => Close(_tertiaryAction));

            // Hide when left active in the scene. When the object starts inactive, Awake
            // first runs inside Show()'s SetActive(true) and must not hide it again.
            if (!_visible)
                gameObject.SetActive(false);
        }

        public void ShowYesNo(string message, Action onYes, Action onNo = null)
            => Show(message, yesNoGroup, onYes, onNo);

        public void ShowDeleteCancel(string message, Action onDelete, Action onCancel = null)
            => Show(message, deleteCancelGroup, onDelete, onCancel);

        public void ShowDone(string message, Action onDone = null)
            => Show(message, doneGroup, onDone, null);

        /// <summary>Account menu: Log out (primary), Exit (third), Cancel (secondary).</summary>
        public void ShowAccount(string message, Action onLogout, Action onExit, Action onCancel = null)
            => Show(message, accountGroup, onLogout, onCancel, onExit);

        /// <summary>
        /// Replaces the message of the open popup if it is showing the Account menu
        /// (e.g. once the account has been fetched). Ignored otherwise.
        /// </summary>
        public void UpdateAccountMessage(string message)
        {
            if (_visible && _activeGroup == accountGroup && accountGroup != null)
                messageText.text = message;
        }

        /// <summary>Dismisses the popup as if No / Cancel was pressed.</summary>
        public void Hide() => Close(_secondaryAction);

        private void OnPrimary() => Close(_primaryAction);

        private void OnSecondary() => Close(_secondaryAction);

        // Hide before invoking, so a callback that opens the next popup is not closed by this
        // one: the fade-out starts first, and a Show from the callback simply reverses it.
        private void Close(Action action)
        {
            _visible = false;
            _primaryAction = null;
            _secondaryAction = null;
            _tertiaryAction = null;

            // Stop taking clicks at once; the panel only stays up to fade out.
            Group.blocksRaycasts = false;
            Group.interactable   = false;
            if (gameObject.activeInHierarchy && fadeOutDuration > 0f)
                StartFade(0f, fadeOutDuration, deactivateAtEnd: true);
            else
                gameObject.SetActive(false);

            action?.Invoke();
        }

        private void StartFade(float target, float duration, bool deactivateAtEnd)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(target, duration, deactivateAtEnd));
        }

        // Fades from the current alpha, so reversing a half-finished fade does not jump.
        private System.Collections.IEnumerator FadeRoutine(float target, float duration, bool deactivateAtEnd)
        {
            float from = Group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(from, target, elapsed / duration);
                yield return null;
            }
            Group.alpha = target;
            _fade = null;
            if (deactivateAtEnd) gameObject.SetActive(false);
        }

        private void Show(string message, GameObject activeGroup, Action primary, Action secondary,
            Action tertiary = null)
        {
            if (_visible)
            {
                var replaced = _secondaryAction;
                _primaryAction = null;
                _secondaryAction = null;
                _tertiaryAction = null;
                replaced?.Invoke();
            }

            messageText.text = message;
            _primaryAction = primary;
            _secondaryAction = secondary;
            _tertiaryAction = tertiary;
            _activeGroup = activeGroup;

            yesNoGroup.SetActive(yesNoGroup == activeGroup);
            deleteCancelGroup.SetActive(deleteCancelGroup == activeGroup);
            doneGroup.SetActive(doneGroup == activeGroup);
            if (accountGroup != null) accountGroup.SetActive(accountGroup == activeGroup);

            bool wasHidden = !gameObject.activeSelf;
            _visible = true;
            gameObject.SetActive(true);

            Group.blocksRaycasts = true;
            Group.interactable   = true;
            if (wasHidden) Group.alpha = 0f;   // else continue from a fade-out in progress
            // A coroutine cannot start under an inactive parent; show fully instead of
            // leaving an invisible popup that still blocks input.
            if (fadeInDuration > 0f && gameObject.activeInHierarchy)
                StartFade(1f, fadeInDuration, deactivateAtEnd: false);
            else
            {
                if (_fade != null) { StopCoroutine(_fade); _fade = null; }
                Group.alpha = 1f;
            }
        }
    }
}
