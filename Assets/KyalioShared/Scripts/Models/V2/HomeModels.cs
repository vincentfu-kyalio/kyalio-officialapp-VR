using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Roles section render mode. Controls which collection inside HomeRoleItem is populated.
    /// </summary>
    public static class HomeRolesDisplayMode
    {
        public const string Projects = "projects";
        public const string Episodes = "episodes";
    }

    /// <summary>
    /// Response for GET /api/me/home. Body contains IDs only — clients hydrate each
    /// projectId against their local Project cache (populated via /api/projects/batch).
    /// </summary>
    public class HomeResponse
    {
        [JsonProperty("thumbnailsExpireAt")]
        public string ThumbnailsExpireAt;

        [JsonProperty("latest")]
        public List<string> Latest;

        [JsonProperty("recommended")]
        public List<string> Recommended;

        [JsonProperty("roles")]
        public HomeRoles Roles;

        [JsonProperty("filters")]
        public HomeFilters Filters;
    }

    public class HomeRoles
    {
        /// <summary>One of <see cref="HomeRolesDisplayMode"/> constants.</summary>
        [JsonProperty("displayMode")]
        public string DisplayMode;

        [JsonProperty("items")]
        public List<HomeRoleItem> Items;
    }

    public class HomeRoleItem
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        /// <summary>Populated when displayMode == "projects".</summary>
        [JsonProperty("projectIds")]
        public List<string> ProjectIds;

        /// <summary>Populated when displayMode == "episodes".</summary>
        [JsonProperty("episodes")]
        public List<EpisodeRef> Episodes;
    }

    /// <summary>
    /// Normalized catalog scoped to the member's currently granted projects.
    /// These are the only specialty / program IDs accepted by /api/projects/search.
    /// </summary>
    public class HomeFilters
    {
        [JsonProperty("specialties")]
        public List<IdNameRef> Specialties;

        [JsonProperty("programs")]
        public List<ProgramSummary> Programs;
    }
}
