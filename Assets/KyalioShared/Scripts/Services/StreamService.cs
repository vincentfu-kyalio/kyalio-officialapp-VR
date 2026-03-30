using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;

namespace Kyalio.Services
{
    public class StreamService
    {
        private readonly ApiClient _client;

        public StreamService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// GET /api/projects/{projectId}/videos/{videoId}/stream
        /// Returns a signed stream URL, token, and expiresAt.
        /// </summary>
        public UniTask<StreamResponse> GetStreamAsync(
            string projectId,
            string videoId,
            CancellationToken ct = default)
        {
            var path = $"/api/projects/{projectId}/videos/{videoId}/stream";
            UnityEngine.Debug.Log($"[StreamService] GET {path} | projectId={projectId} videoId={videoId}");
            return _client.GetAsync<StreamResponse>(path, ct);
        }
    }
}
