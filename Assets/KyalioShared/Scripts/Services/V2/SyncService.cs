using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Delta-sync endpoints: granted-project versions and member watch progress.
    /// Call SyncAsync on login / resume; diff its grantedProjects against local cache
    /// to decide which projectIds to feed into ContentService.BatchAsync.
    /// </summary>
    public class SyncService
    {
        private readonly ApiClient _client;

        public SyncService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>GET /api/me/sync — authoritative granted-project version snapshot.</summary>
        public UniTask<SyncResponse> SyncAsync(CancellationToken ct = default)
            => _client.GetAsync<SyncResponse>("/api/me/sync", ct);

        /// <summary>
        /// GET /api/me/progress — full snapshot when <paramref name="since"/> is null,
        /// delta otherwise. Pass the previous response's timestamp as <paramref name="since"/>
        /// on resume.
        /// </summary>
        public UniTask<ProgressResponse> GetProgressAsync(string since = null, CancellationToken ct = default)
        {
            var path = string.IsNullOrEmpty(since)
                ? "/api/me/progress"
                : $"/api/me/progress?since={Uri.EscapeDataString(since)}";
            return _client.GetAsync<ProgressResponse>(path, ct);
        }
    }
}
