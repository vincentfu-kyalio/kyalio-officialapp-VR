using Kyalio.Models;
using Kyalio.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Full-screen blocking overlay shown when the app version is no longer supported.
    ///
    /// Triggered two ways:
    ///   - Boot: AppBootstrapper calls <see cref="Show(string,string)"/> when the
    ///     version-check endpoint reports updateRequired.
    ///   - Anytime: any endpoint returning 426 fires ApiClient.OnForceUpdateRequired,
    ///     which this component listens for and surfaces.
    ///
    /// Place this on an always-active GameObject that sits above every page. Assign
    /// <c>root</c> to the panel to toggle (start it inactive in the Inspector).
    ///
    /// Inspector: root, messageText, updateButton, fallbackStoreUrl (optional)
    /// </summary>
    public class ForceUpdateScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button updateButton;

        [Tooltip("Used when the server does not supply a storeUrl (e.g. the Quest store listing).")]
        [SerializeField] private string fallbackStoreUrl;

        private const string DefaultMessage =
            "A new version is required to continue. Please update the app to keep using Kyalio.";

        private string _storeUrl;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (updateButton != null) updateButton.onClick.AddListener(OnUpdateClicked);
        }

        private void OnEnable()  => ApiClient.OnForceUpdateRequired += Show;
        private void OnDisable() => ApiClient.OnForceUpdateRequired -= Show;

        /// <summary>Event-path entry — handles the 426 ForceUpdateInfo body.</summary>
        public void Show(ForceUpdateInfo info)
            => Show(info?.Error?.Message, info?.StoreUrl);

        /// <summary>Core entry — message and store URL may be null/empty.</summary>
        public void Show(string message, string storeUrl)
        {
            _storeUrl = !string.IsNullOrEmpty(storeUrl) ? storeUrl : fallbackStoreUrl;

            if (messageText != null)
                messageText.text = string.IsNullOrEmpty(message) ? DefaultMessage : message;

            if (updateButton != null)
                updateButton.gameObject.SetActive(!string.IsNullOrEmpty(_storeUrl));

            if (root != null) root.SetActive(true);

            // Blocking overlay — keep it on top of whatever page is showing.
            transform.SetAsLastSibling();
        }

        private void OnUpdateClicked()
        {
            if (!string.IsNullOrEmpty(_storeUrl))
                Application.OpenURL(_storeUrl);
        }
    }
}
