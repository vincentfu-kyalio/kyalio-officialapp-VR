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

        [Header("Transition")]
        [SerializeField, Min(0f)] private float fadeInDuration  = 0.15f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.1f;

        private Action _primaryAction;
        private Action _secondaryAction;
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

        private void Show(string message, GameObject activeGroup, Action primary, Action secondary)
        {
            if (_visible)
            {
                var replaced = _secondaryAction;
                _primaryAction = null;
                _secondaryAction = null;
                replaced?.Invoke();
            }

            messageText.text = message;
            _primaryAction = primary;
            _secondaryAction = secondary;

            yesNoGroup.SetActive(yesNoGroup == activeGroup);
            deleteCancelGroup.SetActive(deleteCancelGroup == activeGroup);
            doneGroup.SetActive(doneGroup == activeGroup);

            bool wasHidden = !gameObject.activeSelf;
            _visible = true;
            gameObject.SetActive(true);

            Group.blocksRaycasts = true;
            Group.interactable   = true;
            if (wasHidden) Group.alpha = 0f;   // else continue from a fade-out in progress
            if (fadeInDuration > 0f) StartFade(1f, fadeInDuration, deactivateAtEnd: false);
            else
            {
                if (_fade != null) { StopCoroutine(_fade); _fade = null; }
                Group.alpha = 1f;
            }
        }
    }
}
