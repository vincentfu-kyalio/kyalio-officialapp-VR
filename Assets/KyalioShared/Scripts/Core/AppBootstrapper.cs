using System;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Dev;
using Kyalio.Services;
using UnityEngine;

namespace Kyalio.Core
{
    /// <summary>
    /// Single scene entry point for the Quest app.
    /// Place this MonoBehaviour in the scene alongside UIManager.
    ///
    /// Responsibilities:
    ///   1. Initialise ServiceLocator with the API base URL and Quest key.
    ///   2. Navigate to the first page:
    ///        - Real mode  → PageType.Login  (Quest pairing flow)
    ///        - Dev mode   → _devStartPage   (skip login, use fake data)
    ///
    /// Inspector:
    ///   _apiBaseUrl      — API server base URL (e.g. https://your-worker.workers.dev)
    ///   _questAppApiKey  — X-Quest-Key value; leave empty only when dev mode is on
    ///   _useFakeData     — enable fake-data dev mode
    ///   _devStartPage    — page to open directly in dev mode
    ///
    /// Script Execution Order: set this to run BEFORE Default Time (e.g. -10) so
    /// ServiceLocator is ready before any page's Awake() tries to use it.
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        [Header("API Config")]
        [SerializeField] private string _apiBaseUrl = "http://127.0.0.1:8787";
        [SerializeField] private string _questAppApiKey;

        [Header("Force Update")]
        [SerializeField] private ForceUpdateScreen _forceUpdateScreen;

        [Header("Dev")]
        [SerializeField] private bool _useFakeData;
        [SerializeField] private PageType _devStartPage = PageType.Home;

        private void Awake()
        {
            ServiceLocator.Instance.Initialize(_apiBaseUrl, string.Empty, _questAppApiKey);
            DownloadManager.Instance?.Initialize(_apiBaseUrl);
            DevFlags.UseFakeData = _useFakeData;
        }

        private void OnEnable()  => ApiClient.OnUnauthorized += OnUnauthorized;
        private void OnDisable() => ApiClient.OnUnauthorized -= OnUnauthorized;

        private static void OnUnauthorized() => Session.ExpireToLogin();

        private void Start()
        {
            if (_useFakeData)
            {
                FakeDataSeeder.Seed();
                UIManager.Instance.GoTo(_devStartPage);
                return;
            }

            BootAsync().Forget();
        }

        /// <summary>
        /// Real-mode boot: gate on the app version before showing the pairing screen.
        /// A blocking force-update overlay is shown when an update is mandatory; transient
        /// version-check failures fail open so a flaky network can't brick the app.
        /// </summary>
        private async UniTaskVoid BootAsync()
        {
            try
            {
                var version = await ServiceLocator.Instance.V2.Auth.CheckVersionAsync();
                if (version != null && version.UpdateRequired)
                {
                    ShowForceUpdate(version.StoreUrl);
                    return;
                }
            }
            catch (ApiException e) when (e.StatusCode == 426)
            {
                // 426 already fired ApiClient.OnForceUpdateRequired → the overlay handles it.
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AppBootstrapper] Version check failed (continuing): {e.Message}");
            }

            UIManager.Instance.GoTo(PageType.Login);
        }

        private void ShowForceUpdate(string storeUrl)
        {
            if (_forceUpdateScreen != null)
                _forceUpdateScreen.Show(null, storeUrl);
            else
                Debug.LogWarning("[AppBootstrapper] Update required but no ForceUpdateScreen assigned.");
        }
    }
}
