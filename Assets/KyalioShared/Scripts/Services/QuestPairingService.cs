using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;

namespace Kyalio.Services
{
    /// <summary>
    /// Handles the Quest pairing flow:
    ///   POST /api/pair/request  — request a 6-digit code
    ///   GET  /api/pair/poll/{code} — poll until verified
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
        /// GET /api/pair/poll/{code} — returns pending or verified.
        /// Throws ApiException(404) when the code has expired or already been consumed.
        /// </summary>
        public UniTask<PairPollResponse> PollAsync(string code, CancellationToken ct = default)
            => _client.GetAsync<PairPollResponse>($"/api/pair/poll/{code}",
                _questKeyHeader, ct);
    }
}
