using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using Newtonsoft.Json;
using UnityEngine;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Watch-history list + watch-progress upsert.
    /// PATCH supports optimistic concurrency via knownProgressUpdatedAt — 409 returns
    /// the server's current record so the caller can adopt it.
    /// </summary>
    public class WatchHistoryService
    {
        private readonly ApiClient _client;

        public WatchHistoryService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// GET /api/watch-history — mode = "video" (default) or "project".
        /// Response is a PagedResponse&lt;WatchHistoryItem&gt;.
        /// </summary>
        public UniTask<PagedResponse<WatchHistoryItem>> GetHistoryAsync(
            string mode = WatchHistoryMode.Video,
            int? limit = null,
            int? page = null,
            CancellationToken ct = default)
        {
            var path = $"/api/watch-history?mode={mode}";
            if (limit.HasValue) path += $"&limit={limit.Value}";
            if (page.HasValue)  path += $"&page={page.Value}";
            return _client.GetAsync<PagedResponse<WatchHistoryItem>>(path, ct);
        }

        /// <summary>
        /// PATCH /api/watch-history/{videoId}. IsStale = true means the server had
        /// newer data (409) and the caller should adopt the returned record.
        /// </summary>
        public async UniTask<(ProgressItem Record, bool IsStale)> UpdateProgressAsync(
            string videoId, UpdateWatchProgressRequest request, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.PatchAsync<ProgressItem>(
                    $"/api/watch-history/{videoId}", request, ct);
                return (response, false);
            }
            catch (ApiException e) when (e.StatusCode == 409)
            {
                try
                {
                    var serverRecord = JsonConvert.DeserializeObject<ProgressItem>(e.Message);
                    return (serverRecord, true);
                }
                catch
                {
                    Debug.LogWarning($"[WatchHistoryService] 409 body parse failed: {e.Message}");
                    return (null, true);
                }
            }
        }
    }
}
