using System.Collections.Generic;
using System.Threading;
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

        /// <summary>
        /// Most recent Recently Watched list (mode = project), or null before the first
        /// fetch. Filled by <see cref="Kyalio.Core.WatchHistoryLoader"/>.
        /// </summary>
        public List<WatchHistoryItem> WatchHistory { get; private set; }

        /// <summary>Cancelled on <see cref="Reset"/>: background work for this session stops at logout.</summary>
        public CancellationToken SessionToken => _sessionCts.Token;
        private readonly CancellationTokenSource _sessionCts = new();

        public void SetLoggedIn() => IsLoggedIn = true;

        public void SetWatchHistory(List<WatchHistoryItem> items)
        {
            WatchHistory = items;
            WatchHistoryDirty = false;
        }

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
        public void ClearFavoritesDirty()     => FavoritesDirty    = false;

        public static void Reset()
        {
            _instance?._sessionCts.Cancel();
            _instance = new AppState();
            ProjectCacheRepository.Reset();
        }
    }
}
