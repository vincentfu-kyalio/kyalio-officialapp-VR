using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// R2 download lifecycle:
    ///   GET    /download           → presigned URL (2-hour TTL, supports Range for resume)
    ///   POST   /download/complete  → local file fully written
    ///   POST   /download/cancel    → user canceled / download failed
    ///   DELETE /download           → local file deleted (idempotent)
    /// All lifecycle calls return 204 No Content.
    /// </summary>
    public class MediaDownloadService
    {
        private readonly ApiClient _client;

        public MediaDownloadService(ApiClient client)
        {
            _client = client;
        }

        public UniTask<DownloadUrlResponse> GetDownloadUrlAsync(
            string projectId, string videoId, CancellationToken ct = default)
            => _client.GetAsync<DownloadUrlResponse>(BasePath(projectId, videoId), ct);

        public UniTask MarkCompleteAsync(string projectId, string videoId, CancellationToken ct = default)
            => _client.PostAsync($"{BasePath(projectId, videoId)}/complete", ct);

        public UniTask MarkCanceledAsync(string projectId, string videoId, CancellationToken ct = default)
            => _client.PostAsync($"{BasePath(projectId, videoId)}/cancel", ct);

        public UniTask DeleteAsync(string projectId, string videoId, CancellationToken ct = default)
            => _client.DeleteAsync(BasePath(projectId, videoId), ct);

        private static string BasePath(string projectId, string videoId)
            => $"/api/projects/{projectId}/videos/{videoId}/download";
    }
}
