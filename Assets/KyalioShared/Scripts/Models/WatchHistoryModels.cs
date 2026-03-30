using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    // ── GET /api/watch-history?mode=project ───────────────────────────

    public class WatchHistoryProjectItem
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("projectName")]
        public string ProjectName;

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("categoryName")]
        public string CategoryName;

        [JsonProperty("categoryPicUrl")]
        public string CategoryPicUrl;

        [JsonProperty("programPicUrl")]
        public string ProgramPicUrl;

        [JsonProperty("latestEpisode")]
        public WatchHistoryLatestEpisode LatestEpisode;
    }

    public class WatchHistoryLatestEpisode
    {
        [JsonProperty("mediaVideoId")]
        public string MediaVideoId;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("durationMs")]
        public int DurationMs;

        [JsonProperty("ordinal")]
        public int Ordinal;

        [JsonProperty("serverUpdatedAt")]
        public string ServerUpdatedAt;
    }

    public class WatchHistoryProjectResponse
    {
        [JsonProperty("items")]
        public List<WatchHistoryProjectItem> Items;

        [JsonProperty("page")]
        public int Page;

        [JsonProperty("hasMore")]
        public bool HasMore;
    }

    // ── PATCH /api/watch-history/{mediaVideoId} ───────────────────────

    public class WatchProgressRequest
    {
        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("deviceType")]
        public string DeviceType;

        /// <summary>
        /// serverUpdatedAt from the last successful sync. Null on first sync.
        /// </summary>
        [JsonProperty("knownServerUpdatedAt")]
        public string KnownServerUpdatedAt;
    }

    public class WatchProgressResponse
    {
        [JsonProperty("mediaVideoId")]
        public string MediaVideoId;

        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("serverUpdatedAt")]
        public string ServerUpdatedAt;

        [JsonProperty("lastDeviceType")]
        public string LastDeviceType;
    }
}
