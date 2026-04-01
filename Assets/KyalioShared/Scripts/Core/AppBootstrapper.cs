using Kyalio.Dev;
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

        [Header("Dev")]
        [SerializeField] private bool _useFakeData;
        [SerializeField] private PageType _devStartPage = PageType.Home;

        private void Awake()
        {
            ServiceLocator.Instance.Initialize(_apiBaseUrl, string.Empty, _questAppApiKey);
            DevFlags.UseFakeData = _useFakeData;
        }

        private void Start()
        {
            if (_useFakeData)
            {
                FakeDataSeeder.Seed();
                UIManager.Instance.GoTo(_devStartPage);
            }
            else
            {
                UIManager.Instance.GoTo(PageType.Login);
            }
        }
    }
}
