using System.Collections.Generic;
using Kyalio.Models;
using Kyalio.Repositories;

namespace Kyalio.State
{
    /// <summary>
    /// Global app state after login; lives for the entire session.
    /// Call Reset() on logout.
    /// </summary>
    public class AppState
    {
        private static AppState _instance;
        public static AppState Instance => _instance ??= new AppState();

        // ── Login Info ────────────────────────────────────────────────

        public bool IsLoggedIn { get; private set; }

        public void SetLoggedIn()
        {
            IsLoggedIn = true;
        }

        // ── Subscription Content ──────────────────────────────────────

        public List<SubscriptionItem> Subscriptions { get; private set; } = new();
        public bool HasSubscriptions => Subscriptions.Count > 0;

        public void SetSubscriptions(List<SubscriptionItem> subscriptions)
        {
            Subscriptions = subscriptions ?? new();
            ProjectCacheRepository.Instance.Build(Subscriptions);
        }

        // ── Dirty flags ───────────────────────────────────────────────

        /// <summary>True when watch history may have changed since the last fetch.</summary>
        public bool WatchHistoryDirty { get; private set; } = true;

        /// <summary>True when the favorites list may have changed since the last fetch.</summary>
        public bool FavoritesDirty { get; private set; } = true;

        public void MarkWatchHistoryDirty()   => WatchHistoryDirty = true;
        public void MarkFavoritesDirty()      => FavoritesDirty    = true;
        public void ClearWatchHistoryDirty()  => WatchHistoryDirty = false;
        public void ClearFavoritesDirty()     => FavoritesDirty    = false;

        // ── Reset ─────────────────────────────────────────────────────

        public static void Reset()
        {
            _instance = new AppState();
            ProjectCacheRepository.Reset();
        }
    }
}
