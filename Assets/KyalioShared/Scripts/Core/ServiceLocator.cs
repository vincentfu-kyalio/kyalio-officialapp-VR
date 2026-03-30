using Kyalio.Services;

namespace Kyalio.Core
{
    /// <summary>
    /// Lightweight Service Locator that centralises all service instances.
    /// Initialised by AppManager; Pages and Components access services through this.
    /// </summary>
    public class ServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        public string ApiBaseUrl { get; private set; }
        public ApiClient ApiClient { get; private set; }
        public AuthService AuthService { get; private set; }
        public ProjectService ProjectService { get; private set; }
        public FavoriteService FavoriteService { get; private set; }
        public WatchHistoryService WatchHistoryService { get; private set; }
        public AnalyticsService AnalyticsService { get; private set; }
        public StreamService StreamService { get; private set; }

        public void Initialize(string apiBaseUrl, string appKey)
        {
            ApiBaseUrl = apiBaseUrl.TrimEnd('/');
            ApiClient = new ApiClient(apiBaseUrl);
            AuthService = new AuthService(ApiClient, appKey);
            ProjectService = new ProjectService(ApiClient);
            FavoriteService = new FavoriteService(ApiClient);
            WatchHistoryService = new WatchHistoryService(ApiClient);
            AnalyticsService = new AnalyticsService(ApiClient);
            StreamService = new StreamService(ApiClient);
        }

        public static void Reset() => _instance = null;
    }
}
