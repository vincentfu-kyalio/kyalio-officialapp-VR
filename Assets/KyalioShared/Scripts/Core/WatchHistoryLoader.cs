using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Models.V2;
using Kyalio.Utils;
using UnityEngine;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Core
{
    /// <summary>
    /// Fetches the Recently Watched list into <see cref="AppState.WatchHistory"/>.
    ///
    /// <see cref="Prefetch"/> runs right after login, so My Kyalio has the list and the
    /// first screen of thumbnails ready before it is opened. Concurrent fetches share one
    /// request: opening My Kyalio while the prefetch is still running waits for it
    /// instead of asking again.
    /// </summary>
    public static class WatchHistoryLoader
    {
        public const int Limit = 20;

        // Roughly the first screen of Recently Watched cards.
        private const int PrefetchImageCount = 8;

        private static UniTask<List<WatchHistoryItem>> _inFlight;
        private static AppState _inFlightState;

        /// <summary>
        /// Fetches the list and stores it in AppState. Joins a fetch already running for
        /// this session. Cancelled when the session ends.
        /// </summary>
        public static UniTask<List<WatchHistoryItem>> FetchAsync()
        {
            var state = AppState.Instance;
            if (_inFlightState == state && !_inFlight.Status.IsCompleted())
                return _inFlight;

            _inFlightState = state;
            _inFlight = FetchCoreAsync(state).Preserve();
            return _inFlight;
        }

        /// <summary>Fire and forget: fetches the list, then warms the cache with its first images.</summary>
        public static void Prefetch() => PrefetchAsync().Forget();

        private static async UniTask<List<WatchHistoryItem>> FetchCoreAsync(AppState state)
        {
            var response = await ServiceLocator.Instance.V2.WatchHistory.GetHistoryAsync(
                mode: WatchHistoryMode.Project, limit: Limit, ct: state.SessionToken);
            var items = response?.Items ?? new List<WatchHistoryItem>();

            // A logout during the request replaced the state; drop the old account's list.
            if (state == AppState.Instance) state.SetWatchHistory(items);
            return items;
        }

        private static async UniTaskVoid PrefetchAsync()
        {
            var ct = AppState.Instance.SessionToken;
            try
            {
                var items = await FetchAsync();

                var urls = items.Take(PrefetchImageCount)
                    .SelectMany(item => new[]
                    {
                        RecentlyWatchedCard.ThumbnailUrlFor(item),
                        RecentlyWatchedCard.ProgramLogoUrlFor(item),
                    })
                    .Where(url => url != null)
                    .Distinct()
                    .ToList();
                await ThumbnailLoader.PrefetchAsync(urls, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // Non-fatal: My Kyalio fetches again when it opens.
                Debug.LogWarning($"[WatchHistoryLoader] Prefetch failed: {e.Message}");
            }
        }
    }
}
