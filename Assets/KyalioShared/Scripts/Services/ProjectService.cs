using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;

namespace Kyalio.Services
{
    public class ProjectService
    {
        private readonly ApiClient _client;

        public ProjectService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// GET /api/projects/latest?limit={limit}
        /// </summary>
        public UniTask<List<SubscribedProject>> GetLatestAsync(
            int limit = 5,
            CancellationToken ct = default)
        {
            return _client.GetAsync<List<SubscribedProject>>(
                $"/api/projects/latest?limit={limit}", ct);
        }

        /// <summary>
        /// GET /api/projects/recommended
        /// </summary>
        public UniTask<List<FeaturedProject>> GetRecommendedAsync(
            CancellationToken ct = default)
        {
            return _client.GetAsync<List<FeaturedProject>>(
                "/api/projects/recommended", ct);
        }

        /// <summary>
        /// GET /api/roles/projects
        /// </summary>
        public UniTask<List<RoleWithProjects>> GetProjectsByRoleAsync(
            CancellationToken ct = default)
        {
            return _client.GetAsync<List<RoleWithProjects>>(
                "/api/roles/projects", ct);
        }

        /// <summary>
        /// GET /api/roles/content
        /// </summary>
        public UniTask<RoleContentResponse> GetRoleContentAsync(
            CancellationToken ct = default)
        {
            return _client.GetAsync<RoleContentResponse>("/api/roles/content", ct);
        }

        /// <summary>
        /// GET /api/projects/search
        /// All parameters are optional. deviceType is stored for analytics.
        /// </summary>
        public UniTask<SearchResponse> SearchAsync(
            string keyword = null,
            IEnumerable<string> categoryIds = null,
            IEnumerable<string> programIds = null,
            string deviceType = null,
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
            AppendMany("category", categoryIds);
            AppendMany("program", programIds);
            if (!string.IsNullOrEmpty(deviceType)) Append("deviceType", deviceType);

            return _client.GetAsync<SearchResponse>(sb.ToString(), ct);
        }

        /// <summary>
        /// GET /api/projects/{projectId}
        /// </summary>
        public UniTask<ProjectDetail> GetProjectDetailAsync(
            string projectId,
            CancellationToken ct = default)
        {
            return _client.GetAsync<ProjectDetail>(
                $"/api/projects/{projectId}", ct);
        }
    }
}
