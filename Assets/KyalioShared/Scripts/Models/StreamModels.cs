using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class StreamResponse
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("projectVideoId")]
        public int ProjectVideoId;

        [JsonProperty("videoId")]
        public string VideoId;

        [JsonProperty("videoName")]
        public string VideoName;

        [JsonProperty("token")]
        public string Token;

        [JsonProperty("streamUrl")]
        public string StreamUrl;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;        // ISO 8601; parsed by StreamExpiryChecker

        [JsonProperty("projectionType")]
        public string ProjectionType;

        [JsonProperty("stereoLayout")]
        public string StereoLayout;

        [JsonProperty("eyeOrder")]
        public string EyeOrder;
    }
}
