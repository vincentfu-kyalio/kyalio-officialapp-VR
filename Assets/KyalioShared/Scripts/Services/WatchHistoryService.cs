using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace Kyalio.Services
{
    /// <summary>
    /// GET /api/watch-history?mode=project  — recently watched projects
    /// PATCH /api/watch-history/{mediaVideoId} — upsert watch progress (optimistic locking)
    /// </summary>
    public class WatchHistoryService
    {
        private readonly ApiClient _client;

        public WatchHistoryService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Returns one item per project (most recently watched episode), sorted by serverUpdatedAt DESC.
        /// </summary>
        public UniTask<WatchHistoryProjectResponse> GetProjectHistoryAsync(
            int limit = 20, CancellationToken ct = default)
        {
            return _client.GetAsync<WatchHistoryProjectResponse>(
                $"/api/watch-history?mode=project&limit={limit}", ct);
        }

        /// <summary>
        /// Upserts watch progress. Returns the current server record.
        /// IsStale = true when the server had newer data (409); caller should accept server's value.
        /// </summary>
        public async UniTask<(WatchProgressResponse Data, bool IsStale)> UpdateProgressAsync(
            string mediaVideoId, WatchProgressRequest request, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.PatchAsync<WatchProgressResponse>(
                    $"/api/watch-history/{mediaVideoId}", request, ct);
                return (response, false);
            }
            catch (ApiException e) when (e.StatusCode == 409)
            {
                // Server has newer data — deserialize server record from response body
                try
                {
                    var serverRecord = JsonConvert.DeserializeObject<WatchProgressResponse>(e.Message);
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
