using Kyalio.Services;
using V2 = Kyalio.Services.V2;

namespace Kyalio.Core
{
    /// <summary>
    /// Lightweight Service Locator that centralises all service instances.
    /// Initialised by AppManager; Pages and Components access services through this.
    ///
    /// All services follow the V2 (delta-sync) contract and are exposed under <see cref="V2"/>.
    /// </summary>
    public class ServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        public string ApiBaseUrl { get; private set; }
        public ApiClient ApiClient { get; private set; }

        // ── Services (new contract) ──────────────────────────────────
        public V2Services V2 { get; private set; }

        public class V2Services
        {
            public V2.AuthService Auth { get; internal set; }
            public V2.SyncService Sync { get; internal set; }
            public V2.HomeService Home { get; internal set; }
            public V2.ContentService Content { get; internal set; }
            public V2.StreamService Stream { get; internal set; }
            public V2.MediaDownloadService Download { get; internal set; }
            public V2.WatchHistoryService WatchHistory { get; internal set; }
            public V2.FavoriteService Favorites { get; internal set; }
            public V2.AnalyticsService Analytics { get; internal set; }
        }

        /// <summary>
        /// Initialise all services.
        /// appKey     — X-App-Key for the mobile auth surface (pass empty string on Quest).
        /// questKey   — X-Quest-Key for the Quest pairing surface (pass empty string on Mobile).
        /// appVersion — X-App-Version sent on every non-admin endpoint except logout.
        ///              Must be major.minor.patch; defaults to Application.version.
        /// </summary>
        public void Initialize(string apiBaseUrl, string appKey, string questKey = "", string appVersion = null)
        {
            ApiBaseUrl = apiBaseUrl.TrimEnd('/');
            ApiClient = new ApiClient(apiBaseUrl, appVersion ?? UnityEngine.Application.version);

            V2 = new V2Services
            {
                Auth         = new V2.AuthService(ApiClient, appKey, questKey),
                Sync         = new V2.SyncService(ApiClient),
                Home         = new V2.HomeService(ApiClient),
                Content      = new V2.ContentService(ApiClient),
                Stream       = new V2.StreamService(ApiClient),
                Download     = new V2.MediaDownloadService(ApiClient),
                WatchHistory = new V2.WatchHistoryService(ApiClient),
                Favorites    = new V2.FavoriteService(ApiClient),
                Analytics    = new V2.AnalyticsService(ApiClient),
            };
        }

        public static void Reset() => _instance = null;
    }
}
