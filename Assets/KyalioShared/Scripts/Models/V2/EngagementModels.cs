using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    // ── Favorites ────────────────────────────────────────────────────

    public class FavoriteItem
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("favoritedAt")]
        public string FavoritedAt;
    }

    public class FavoritesResponse
    {
        [JsonProperty("items")]
        public List<FavoriteItem> Items;
    }

    // ── Watch history list (video / project mode) ────────────────────

    public static class WatchHistoryMode
    {
        public const string Video   = "video";
        public const string Project = "project";
    }

    /// <summary>
    /// Single watch-history record. The response is identical between video mode
    /// (one record per video) and project mode (latest video per project).
    /// </summary>
    public class WatchHistoryItem
    {
        [JsonProperty("videoId")]
        public string VideoId;

        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("lastDeviceType")]
        public string LastDeviceType;

        [JsonProperty("progressUpdatedAt")]
        public string ProgressUpdatedAt;
    }

    // ── PATCH /api/watch-history/{videoId} ───────────────────────────

    public class UpdateWatchProgressRequest
    {
        [JsonProperty("progressMs")]
        public int ProgressMs;

        [JsonProperty("projectId")]
        public string ProjectId;

        /// <summary>progressUpdatedAt from the last successful sync. Null on first sync.</summary>
        [JsonProperty("knownProgressUpdatedAt")]
        public string KnownProgressUpdatedAt;
    }

    // ── Analytics ────────────────────────────────────────────────────

    /// <summary>
    /// Allowed values for ProjectPageSessionRequest.Source.
    /// </summary>
    public static class ProjectPageSource
    {
        public const string Latest      = "latest";
        public const string Recommended = "recommended";
        public const string Roles       = "roles";
        public const string Specialty   = "specialty";
        public const string Search      = "search";
        public const string Favorites   = "favorites";
        public const string Direct      = "direct";
    }

    public class ProjectPageSessionRequest
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        /// <summary>One of <see cref="ProjectPageSource"/> constants.</summary>
        [JsonProperty("source")]
        public string Source;

        /// <summary>Required only when Source == "search".</summary>
        [JsonProperty("sourceSearchEventId")]
        public string SourceSearchEventId;

        [JsonProperty("durationMs")]
        public long DurationMs;

        [JsonProperty("videoStarted")]
        public bool VideoStarted;

        [JsonProperty("startedAt")]
        public string StartedAt;
    }

    /// <summary>
    /// Single completed view session — POST /api/analytics/view-sessions (max 50/request).
    /// </summary>
    public class ViewSession
    {
        /// <summary>Client-generated UUID v4. Duplicate uploads are ignored.</summary>
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("videoId")]
        public string VideoId;

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
    }
}
