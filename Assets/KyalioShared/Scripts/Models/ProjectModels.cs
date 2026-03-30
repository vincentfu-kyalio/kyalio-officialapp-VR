using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    /// <summary>
    /// A Project within subscription content (corresponds to the API's PublicProject schema).
    /// The "Public" naming is reserved for future use.
    /// </summary>
    public class SubscribedProject
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("drName")]
        public string DrName;

        [JsonProperty("institution")]
        public string Institution;

        [JsonProperty("tag")]
        public string Tag;

        [JsonProperty("programId")]
        public string ProgramId;

        [JsonProperty("programName")]
        public string ProgramName;

        [JsonProperty("categoryId")]
        public string CategoryId;

        [JsonProperty("categoryName")]
        public string CategoryName;

        [JsonProperty("roleId")]
        public string RoleId;

        [JsonProperty("roleName")]
        public string RoleName;

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("programPicUrl")]
        public string ProgramPicUrl;

        [JsonProperty("playlistCount")]
        public int PlaylistCount;

        [JsonProperty("playlistDurationSeconds")]
        public int PlaylistDurationSeconds;

    }

    /// <summary>
    /// Featured/recommended content with an ordering field (corresponds to the API's FeaturedProject schema).
    /// </summary>
    public class FeaturedProject : SubscribedProject
    {
        [JsonProperty("ordinal")]
        public int Ordinal;

        [JsonProperty("featuredCreatedAt")]
        public string FeaturedCreatedAt;
    }

    /// <summary>
    /// Projects grouped by Role (corresponds to the API's RoleProjects schema).
    /// </summary>
    public class RoleWithProjects
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("projects")]
        public List<SubscribedProject> Projects;
    }

    /// <summary>
    /// Project detail including its playlist (corresponds to the API's ProjectDetail schema).
    /// </summary>
    public class ProjectDetail : SubscribedProject
    {
        [JsonProperty("playlist")]
        public List<PlaylistItem> Playlist;
    }

    /// <summary>
    /// Projects or episodes grouped by Role — returned by GET /api/roles/content.
    /// The backend ROLES_CONTENT_MODE controls which field is populated.
    /// </summary>
    public class RoleContentResponse
    {
        [JsonProperty("mode")]
        public string Mode;   // "projects" or "episodes"

        [JsonProperty("items")]
        public List<RoleContentItem> Items;
    }

    /// <summary>
    /// A single role entry inside RoleContentResponse.
    /// Either Projects or Episodes is populated depending on Mode.
    /// </summary>
    public class RoleContentItem
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("projects")]
        public List<SubscribedProject> Projects;

        [JsonProperty("episodes")]
        public List<RoleContentEpisode> Episodes;
    }

    /// <summary>
    /// A single video item within the playlist.
    /// </summary>
    public class PlaylistItem
    {
        [JsonProperty("id")]
        public int Id;                  // projectVideoId, used for thumbnail

        [JsonProperty("ordinal")]
        public int Ordinal;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("mediaVideoId")]
        public string MediaVideoId;     // videoId, used for streaming

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("videoName")]
        public string VideoName;

        [JsonProperty("durationMs")]
        public int? DurationMs;

        [JsonProperty("progressMs")]
        public int ProgressMs;          // 0 if never watched

        [JsonProperty("serverUpdatedAt")]
        public string ServerUpdatedAt;  // null if never watched

        [JsonProperty("projectionType")]
        public string ProjectionType;   // "360", "180", or null

        [JsonProperty("stereoLayout")]
        public string StereoLayout;     // "sbs", "tb", or null

        [JsonProperty("eyeOrder")]
        public string EyeOrder;

        [JsonProperty("sizeBytes")]
        public long? SizeBytes;         // file size in bytes; null if unknown

        /// <summary>
        /// Whether this is a Side-by-Side stereo video, which determines whether the Cardboard button is shown.
        /// </summary>
        public bool IsSBS => StereoLayout == "sbs";
    }

    /// <summary>
    /// A PlaylistItem enriched with its parent project info — used in episodes mode of GET /api/roles/content.
    /// Extends PlaylistItem so it can be passed directly to VideoPlayerController.Play().
    /// </summary>
    public class RoleContentEpisode : PlaylistItem
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("projectName")]
        public string ProjectName;
    }

    /// <summary>
    /// Response from GET /api/projects/search.
    /// </summary>
    public class SearchResponse
    {
        [JsonProperty("searchEventId")]
        public string SearchEventId;

        [JsonProperty("results")]
        public List<SubscribedProject> Results;
    }

    /// <summary>
    /// Navigation parameter passed to ProjectInfoPage via UIManager.GoTo.
    /// Carries the entry source so the page can report project-page-session analytics.
    /// </summary>
    public class ProjectNavParam
    {
        public string ProjectId;

        /// <summary>
        /// One of: search, latest, recommended, favorites, roles_content, category, direct.
        /// </summary>
        public string Source;

        /// <summary>
        /// Non-null only when Source == "search". The searchEventId from the search response.
        /// </summary>
        public string SearchEventId;
    }
}
