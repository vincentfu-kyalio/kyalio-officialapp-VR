using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using UnityEngine;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Signed Mux playback URL for a single video. streamUrl already carries the
    /// signed token — do not append query parameters.
    /// </summary>
    public class StreamService
    {
        private readonly ApiClient _client;

        public StreamService(ApiClient client)
        {
            _client = client;
        }

        public UniTask<StreamResponse> GetStreamAsync(
            string projectId, string videoId, CancellationToken ct = default)
        {
            var path = $"/api/projects/{projectId}/videos/{videoId}/stream";
            Debug.Log($"[StreamService] GET {path}");
            return _client.GetAsync<StreamResponse>(path, ct);
        }
    }
}
