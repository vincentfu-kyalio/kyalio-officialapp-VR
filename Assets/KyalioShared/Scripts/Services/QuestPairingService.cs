using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using Newtonsoft.Json;

namespace Kyalio.Services
{
    /// <summary>
    /// Handles the Quest pairing flow:
    ///   POST /api/pair/request          — request a 6-digit code
    ///   GET  /api/pair/stream/{code}    — SSE stream until verified or expired
    ///
    /// Both endpoints require the X-Quest-Key header (no Bearer token).
    /// </summary>
    public class QuestPairingService
    {
        private readonly ApiClient _client;
        private readonly IReadOnlyDictionary<string, string> _questKeyHeader;

        public QuestPairingService(ApiClient client, string questKey)
        {
            _client = client;
            _questKeyHeader = new Dictionary<string, string> { ["X-Quest-Key"] = questKey };
        }

        /// <summary>POST /api/pair/request — returns the 6-digit code and its expiry.</summary>
        public UniTask<PairRequestResponse> RequestPairAsync(CancellationToken ct = default)
            => _client.PostAsync<PairRequestResponse>("/api/pair/request", new PairRequestBody(),
                _questKeyHeader, ct);

        /// <summary>
        /// GET /api/pair/stream/{code} — SSE stream until a terminal event arrives.
        /// Returns a <see cref="PairPollResponse"/> with Status "verified" (+ Credential)
        /// or "expired". Throws ApiException(404) if the code is not found before the
        /// stream opens.
        /// </summary>
        public UniTask<PairPollResponse> StreamAsync(string code, CancellationToken ct = default)
            => _client.SseAsync<PairPollResponse>(
                $"/api/pair/stream/{code}",
                _questKeyHeader,
                (eventName, data) => eventName switch
                {
                    "verified" => JsonConvert.DeserializeObject<PairPollResponse>(data),
                    "expired"  => new PairPollResponse { Status = "expired" },
                    _          => null  // "pending" — keep listening
                },
                ct);
    }
}
