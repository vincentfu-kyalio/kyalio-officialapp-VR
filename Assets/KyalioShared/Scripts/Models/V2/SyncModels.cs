using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Per-project version pair returned from /api/me/sync.
    /// </summary>
    public class GrantedProjectVersion
    {
        [JsonProperty("projectVersion")]
        public int ProjectVersion;

        [JsonProperty("thumbnailVersion")]
        public int ThumbnailVersion;
    }

    /// <summary>
    /// Response for GET /api/me/sync. grantedProjects is the authoritative set —
    /// any local projectId not present must be purged.
    /// </summary>
    public class SyncResponse
    {
        [JsonProperty("timestamp")]
        public string Timestamp;

        [JsonProperty("grantedProjects")]
        public Dictionary<string, GrantedProjectVersion> GrantedProjects;
    }

    /// <summary>
    /// Response for GET /api/me/progress and GET /api/me/progress?since=...
    /// </summary>
    public class ProgressResponse
    {
        [JsonProperty("timestamp")]
        public string Timestamp;

        [JsonProperty("items")]
        public List<ProgressItem> Items;
    }
}
