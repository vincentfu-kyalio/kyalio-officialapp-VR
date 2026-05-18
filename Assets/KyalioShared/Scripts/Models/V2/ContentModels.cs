using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    // ── POST /api/projects/batch ─────────────────────────────────────

    public class ProjectsBatchRequest
    {
        [JsonProperty("projectIds")]
        public List<string> ProjectIds;
    }

    public class ProjectsBatchResponse
    {
        [JsonProperty("thumbnailsExpireAt")]
        public string ThumbnailsExpireAt;

        [JsonProperty("items")]
        public List<Project> Items;
    }

    // ── GET /api/projects/{projectId} ────────────────────────────────

    /// <summary>
    /// Single-project response. Same fields as Project plus the response-level
    /// thumbnailsExpireAt that governs playlist[].thumbnailUrl validity.
    /// </summary>
    public class ProjectDetailResponse : Project
    {
        [JsonProperty("thumbnailsExpireAt")]
        public string ThumbnailsExpireAt;
    }

    // ── GET /api/projects/search ─────────────────────────────────────

    /// <summary>
    /// Search response — items contains only projectId strings, paginated.
    /// </summary>
    public class SearchResponse
    {
        /// <summary>Null when no filter was supplied. Pass to analytics as sourceSearchEventId.</summary>
        [JsonProperty("searchEventId")]
        public string SearchEventId;

        [JsonProperty("items")]
        public List<string> Items;

        [JsonProperty("page")]
        public int Page;

        [JsonProperty("pageSize")]
        public int PageSize;

        [JsonProperty("hasMore")]
        public bool HasMore;
    }
}
