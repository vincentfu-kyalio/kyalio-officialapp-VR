using Kyalio.Models.V2;
using Kyalio.Repositories.V2;

namespace Kyalio.State.V2
{
    /// <summary>
    /// Global V2 app state for the post-pairing session. Lives until logout.
    /// </summary>
    public class AppState
    {
        private static AppState _instance;
        public static AppState Instance => _instance ??= new AppState();

        public bool IsLoggedIn { get; private set; }
        public HomeResponse LastHome { get; private set; }

        public void SetLoggedIn() => IsLoggedIn = true;

        public void SetHome(HomeResponse home)
        {
            LastHome = home;
            ProjectCacheRepository.Instance.ApplyHome(home);
        }

        // ── Dirty flags ──────────────────────────────────────────────

        public bool WatchHistoryDirty { get; private set; } = true;
        public bool FavoritesDirty { get; private set; } = true;

        public void MarkWatchHistoryDirty()   => WatchHistoryDirty = true;
        public void MarkFavoritesDirty()      => FavoritesDirty    = true;
        public void ClearWatchHistoryDirty()  => WatchHistoryDirty = false;
        public void ClearFavoritesDirty()     => FavoritesDirty    = false;

        public static void Reset()
        {
            _instance = new AppState();
            ProjectCacheRepository.Reset();
        }
    }
}
