using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Utils;
using UnityEngine;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Core
{
    /// <summary>
    /// Fetches My Kyalio's server lists (Recently Watched, Favorites) into AppState.
    ///
    /// <see cref="Prefetch"/> runs right after login, so My Kyalio has both lists and the
    /// first screen of their images ready before it is opened. Concurrent fetches of a
    /// list share one request: opening a tab while the prefetch is still running waits
    /// for it instead of asking again. Everything is cancelled when the session ends.
    /// </summary>
    public static class MyKyalioLoader
    {
        public const int WatchHistoryLimit = 20;

        // Roughly the first screen of cards in each list.
        private const int PrefetchImageCount = 8;

        private static readonly SharedFetch<List<WatchHistoryItem>> s_watchHistory = new();
        private static readonly SharedFetch<List<FavoriteItem>> s_favorites = new();

        /// <summary>Fetches Recently Watched (mode = project) into AppState.WatchHistory.</summary>
        public static UniTask<List<WatchHistoryItem>> FetchWatchHistoryAsync() =>
            s_watchHistory.Run(async state =>
            {
                var response = await ServiceLocator.Instance.V2.WatchHistory.GetHistoryAsync(
                    mode: WatchHistoryMode.Project, limit: WatchHistoryLimit, ct: state.SessionToken);
                var items = response?.Items ?? new List<WatchHistoryItem>();
                if (state == AppState.Instance) state.SetWatchHistory(items);
                return items;
            });

        /// <summary>Fetches Favorites into AppState.Favorites.</summary>
        public static UniTask<List<FavoriteItem>> FetchFavoritesAsync() =>
            s_favorites.Run(async state =>
            {
                var response = await ServiceLocator.Instance.V2.Favorites.GetFavoritesAsync(state.SessionToken);
                var items = response?.Items ?? new List<FavoriteItem>();
                if (state == AppState.Instance) state.SetFavorites(items);
                return items;
            });

        /// <summary>Fire and forget: fetches both lists, then warms the cache with their first images.</summary>
        public static void Prefetch() => PrefetchAsync().Forget();

        private static readonly SharedFetch<MeResponse> s_account = new();

        /// <summary>
        /// Fetches the signed-in account (GET /api/me) into AppState.Account, shown in the
        /// Account menu. Joins a fetch already running for this session.
        /// </summary>
        public static UniTask<MeResponse> FetchAccountAsync() =>
            s_account.Run(async state =>
            {
                var me = await ServiceLocator.Instance.V2.Auth.GetMeAsync(state.SessionToken);
                if (state == AppState.Instance) state.SetAccount(me);
                return me;
            });

        /// <summary>Fire and forget <see cref="FetchAccountAsync"/>; a failure only leaves the account unshown.</summary>
        public static void PrefetchAccount() => PrefetchAccountAsync().Forget();

        private static async UniTaskVoid PrefetchAccountAsync()
        {
            try { await FetchAccountAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[MyKyalioLoader] Account fetch failed: {e.Message}"); }
        }

        private static async UniTaskVoid PrefetchAsync()
        {
            var ct = AppState.Instance.SessionToken;
            try
            {
                var history   = FetchListSafe(FetchWatchHistoryAsync(), "watch history");
                var favorites = FetchListSafe(FetchFavoritesAsync(),    "favorites");
                var (historyItems, favoriteItems) = await UniTask.WhenAll(history, favorites);

                var repo = ProjectCacheRepository.Instance;
                var urls = new List<string>();
                foreach (var item in (historyItems ?? new()).Take(PrefetchImageCount))
                {
                    urls.Add(RecentlyWatchedCard.ThumbnailUrlFor(item));
                    urls.Add(RecentlyWatchedCard.ProgramLogoUrlFor(item));
                }
                // Same images a Favorites row shows (see MyFavoritesPage.BindFavoriteItems).
                foreach (var fav in (favoriteItems ?? new()).Take(PrefetchImageCount))
                {
                    var p = repo.Get(fav.ProjectId);
                    urls.Add(ThumbnailLoader.Resolve(p?.ThumbnailUrl));
                    urls.Add(ThumbnailLoader.Resolve(repo.GetFirstProgram(p)?.PicUrl));
                }

                await ThumbnailLoader.PrefetchAsync(
                    urls.Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList(), ct);
            }
            catch (OperationCanceledException) { }
        }

        // One failed list must not stop the other's prefetch; the page fetches again when opened.
        private static async UniTask<T> FetchListSafe<T>(UniTask<T> fetch, string name) where T : class
        {
            try { return await fetch; }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[MyKyalioLoader] Prefetch {name} failed: {e.Message}");
                return null;
            }
        }

        /// <summary>One in-flight request per session; callers arriving while it runs share it.</summary>
        private sealed class SharedFetch<T>
        {
            private UniTask<T> _task;
            private AppState _state;

            public UniTask<T> Run(Func<AppState, UniTask<T>> fetch)
            {
                var state = AppState.Instance;
                if (_state == state && !_task.Status.IsCompleted())
                    return _task;

                _state = state;
                _task = fetch(state).Preserve();
                return _task;
            }
        }
    }
}
