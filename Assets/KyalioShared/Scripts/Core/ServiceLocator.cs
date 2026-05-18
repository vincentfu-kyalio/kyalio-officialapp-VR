using Kyalio.Services;
using V2 = Kyalio.Services.V2;

namespace Kyalio.Core
{
    /// <summary>
    /// Lightweight Service Locator that centralises all service instances.
    /// Initialised by AppManager; Pages and Components access services through this.
    ///
    /// During the V1 → V2 migration both layers are exposed:
    ///   - V1 properties (AuthService, ProjectService, …) drive existing UI.
    ///   - V2 properties (under <see cref="V2"/>) drive the new sync / home / batch flow.
    /// Once UI fully migrates the V1 properties will be removed.
    /// </summary>
    public class ServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        public string ApiBaseUrl { get; private set; }
        public ApiClient ApiClient { get; private set; }

        // ── V1 (legacy) ──────────────────────────────────────────────
        public AuthService AuthService { get; private set; }
        public QuestPairingService QuestPairingService { get; private set; }
        public ProjectService ProjectService { get; private set; }
        public FavoriteService FavoriteService { get; private set; }
        public WatchHistoryService WatchHistoryService { get; private set; }
        public AnalyticsService AnalyticsService { get; private set; }
        public StreamService StreamService { get; private set; }

        // ── V2 (new contract) ────────────────────────────────────────
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
        /// appKey     — X-App-Key for mobile AuthService (pass empty string on Quest).
        /// questKey   — X-Quest-Key for QuestPairingService (pass empty string on Mobile).
        /// appVersion — X-App-Version sent on every non-admin endpoint except logout.
        ///              Must be major.minor.patch; defaults to Application.version.
        /// </summary>
        public void Initialize(string apiBaseUrl, string appKey, string questKey = "", string appVersion = null)
        {
            ApiBaseUrl = apiBaseUrl.TrimEnd('/');
            ApiClient = new ApiClient(apiBaseUrl, appVersion ?? UnityEngine.Application.version);

            // V1 (legacy) — still backing existing pages until UI migrates.
            AuthService = new AuthService(ApiClient, appKey);
            QuestPairingService = new QuestPairingService(ApiClient, questKey);
            ProjectService = new ProjectService(ApiClient);
            FavoriteService = new FavoriteService(ApiClient);
            WatchHistoryService = new WatchHistoryService(ApiClient);
            AnalyticsService = new AnalyticsService(ApiClient);
            StreamService = new StreamService(ApiClient);

            // V2 (new contract).
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
