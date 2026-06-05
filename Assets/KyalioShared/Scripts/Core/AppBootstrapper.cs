using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Dev;
using Kyalio.Services;
using UnityEngine;
using UnityEngine.XR;

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
        [Tooltip("TabBar root GameObject — must be the same one assigned on LoginPage. " +
                 "Activated in fake-data mode since the login flow that normally enables it is skipped.")]
        [SerializeField] private GameObject _tabBarRoot;

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
            ApplyFoveatedRenderingAsync().Forget();

            if (_useFakeData)
            {
                FakeDataSeeder.Seed();

                // The login flow normally enables the TabBar and selects the tab. Fake mode
                // skips login, so replicate it here — the TabBar root starts inactive, which
                // also means TabBar.Awake (and TabBar.Instance) only runs after SetActive(true).
                if (_tabBarRoot != null)
                    _tabBarRoot.SetActive(true);

                UIManager.Instance.SwitchPage(_devStartPage);
                TabBar.Instance?.SelectTab(_devStartPage);
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

        /// <summary>
        /// Enables fixed foveated rendering (FFR) at full strength once the XR display
        /// subsystem is live. FFR lowers the shading rate toward the periphery of each
        /// eye — detail the lens distortion already blurs — reclaiming GPU budget so head
        /// movement holds native refresh instead of dropping into compositor reprojection,
        /// which is what made the world-space UI judder when turning the head.
        /// The subsystem may not be running on the first frame, so we poll a few seconds.
        /// </summary>
        private static async UniTaskVoid ApplyFoveatedRenderingAsync()
        {
            var displays = new List<XRDisplaySubsystem>();
            for (var attempt = 0; attempt < 180; attempt++)
            {
                SubsystemManager.GetSubsystems(displays);
                var display = displays.Find(d => d.running);
                if (display != null)
                {
                    // 1 = strongest periphery reduction. Fixed (no gaze) — no eye tracking.
                    display.foveatedRenderingLevel = 1f;
                    display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                    return;
                }
                await UniTask.Yield();
            }
            Debug.LogWarning("[AppBootstrapper] XR display subsystem not running; FFR not applied.");
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
