using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class FavoriteItem
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("projectName")]
        public string ProjectName;

        [JsonProperty("categoryName")]
        public string CategoryName;

        [JsonProperty("drName")]
        public string DrName;

        [JsonProperty("programPicUrl")]
        public string ProgramPicUrl;

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl;

        [JsonProperty("playlistDurationSeconds")]
        public int PlaylistDurationSeconds;

        [JsonProperty("videoCount")]
        public int VideoCount;

        [JsonProperty("createdAt")]
        public string CreatedAt;
    }

    public class FavoritesResponse
    {
        [JsonProperty("items")]
        public List<FavoriteItem> Items;
    }
}
