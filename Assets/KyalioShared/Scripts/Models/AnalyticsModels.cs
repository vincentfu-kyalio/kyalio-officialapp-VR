using Newtonsoft.Json;

namespace Kyalio.Models
{
    /// <summary>
    /// Request body for POST /api/analytics/project-page-sessions.
    /// Fire when the user leaves a project page.
    /// </summary>
    public class ProjectPageSessionRequest
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        /// <summary>One of: search, latest, recommended, favorites, roles_content, category, direct.</summary>
        [JsonProperty("source")]
        public string Source;

        [JsonProperty("startedAt")]
        public string StartedAt;

        [JsonProperty("durationMs")]
        public long DurationMs;

        [JsonProperty("videoStarted")]
        public bool VideoStarted;

        /// <summary>Required when Source == "search". The searchEventId from the search response.</summary>
        [JsonProperty("sourceSearchEventId")]
        public string SourceSearchEventId;

        [JsonProperty("deviceType")]
        public string DeviceType;
    }

    /// <summary>
    /// A single completed view session — POST /api/analytics/view-sessions (batch).
    /// </summary>
    public class ViewSession
    {
        /// <summary>Client-generated UUID v4. Duplicate uploads are safely ignored (idempotent).</summary>
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("mediaVideoId")]
        public string MediaVideoId;

        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoTitle")]
        public string VideoTitle;

        [JsonProperty("startedAt")]
        public string StartedAt;

        [JsonProperty("flatWatchMs")]
        public long FlatWatchMs;

        [JsonProperty("cardboardWatchMs")]
        public long CardboardWatchMs;

        [JsonProperty("totalWatchMs")]
        public long TotalWatchMs;

        [JsonProperty("finalProgressMs")]
        public long FinalProgressMs;

        [JsonProperty("durationMs")]
        public long DurationMs;

        [JsonProperty("completed")]
        public bool Completed;

        [JsonProperty("deviceType")]
        public string DeviceType;
    }
}
