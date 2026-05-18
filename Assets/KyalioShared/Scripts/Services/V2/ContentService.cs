using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Project content: batch fetch (delta-driven), single-project detail, search.
    /// </summary>
    public class ContentService
    {
        private readonly ApiClient _client;

        public ContentService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>POST /api/projects/batch — hydrate missing / outdated project IDs.</summary>
        public UniTask<ProjectsBatchResponse> BatchAsync(
            IEnumerable<string> projectIds, CancellationToken ct = default)
        {
            var body = new ProjectsBatchRequest { ProjectIds = new List<string>(projectIds) };
            return _client.PostAsync<ProjectsBatchResponse>("/api/projects/batch", body, null, ct);
        }

        /// <summary>GET /api/projects/{projectId}.</summary>
        public UniTask<ProjectDetailResponse> GetProjectAsync(string projectId, CancellationToken ct = default)
            => _client.GetAsync<ProjectDetailResponse>($"/api/projects/{projectId}", ct);

        /// <summary>
        /// GET /api/projects/search — paged list of projectIds. Hydrate from local cache.
        /// All filters optional; deviceType is no longer accepted by the server.
        /// </summary>
        public UniTask<SearchResponse> SearchAsync(
            string keyword = null,
            IEnumerable<string> specialtyIds = null,
            IEnumerable<string> programIds = null,
            int? page = null,
            int? pageSize = null,
            CancellationToken ct = default)
        {
            var sb = new StringBuilder("/api/projects/search");
            var sep = '?';

            void Append(string key, string value)
            {
                sb.Append(sep).Append(key).Append('=').Append(Uri.EscapeDataString(value));
                sep = '&';
            }

            void AppendMany(string key, IEnumerable<string> values)
            {
                if (values == null) return;
                foreach (var value in values)
                {
                    if (string.IsNullOrEmpty(value)) continue;
                    Append(key, value);
                }
            }

            if (!string.IsNullOrEmpty(keyword)) Append("keyword", keyword);
            AppendMany("specialty", specialtyIds);
            AppendMany("program", programIds);
            if (page.HasValue)     Append("page", page.Value.ToString());
            if (pageSize.HasValue) Append("pageSize", pageSize.Value.ToString());

            return _client.GetAsync<SearchResponse>(sb.ToString(), ct);
        }
    }
}
