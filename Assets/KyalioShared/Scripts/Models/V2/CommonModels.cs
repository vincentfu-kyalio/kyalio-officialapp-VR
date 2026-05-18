using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Normalized program entry inside home filters.
    /// picUrl is a relative auth-gated proxy URL; require Bearer to load.
    /// </summary>
    public class ProgramSummary
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("picUrl")]
        public string PicUrl;

        [JsonProperty("updatedAt")]
        public string UpdatedAt;
    }

    /// <summary>
    /// Generic id+name pair used inside home filters for specialties.
    /// </summary>
    public class IdNameRef
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;
    }

    /// <summary>
    /// Reference to a single episode (used by home roles when displayMode == "episodes").
    /// </summary>
    public class EpisodeRef
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoId")]
        public string VideoId;
    }

    /// <summary>
    /// Server-stored watch progress item — returned by /api/me/progress, /api/watch-history,
    /// and PATCH /api/watch-history/{videoId}.
    /// </summary>
    public class ProgressItem
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoId")]
        public string VideoId;

        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("progressUpdatedAt")]
        public string ProgressUpdatedAt;

        [JsonProperty("lastDeviceType")]
        public string LastDeviceType;
    }

    /// <summary>
    /// Envelope used by all pageable list endpoints.
    /// </summary>
    public class PagedResponse<T>
    {
        [JsonProperty("items")]
        public List<T> Items;

        [JsonProperty("page")]
        public int Page;

        [JsonProperty("pageSize")]
        public int PageSize;

        [JsonProperty("hasMore")]
        public bool HasMore;
    }
}
