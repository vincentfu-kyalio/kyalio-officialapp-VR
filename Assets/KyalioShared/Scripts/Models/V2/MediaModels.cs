using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Response for GET /api/projects/{projectId}/videos/{videoId}/stream.
    /// streamUrl already carries the signed Mux token — do not append extra parameters.
    /// </summary>
    public class StreamResponse
    {
        [JsonProperty("streamUrl")]
        public string StreamUrl;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;

        /// <summary>One of <see cref="PlaybackMode"/> constants.</summary>
        [JsonProperty("playbackMode")]
        public string PlaybackMode;
    }

    /// <summary>
    /// Response for GET /api/projects/{projectId}/videos/{videoId}/download.
    /// downloadUrl is a 2-hour R2 presigned URL; supports Range for resume after refresh.
    /// </summary>
    public class DownloadUrlResponse
    {
        [JsonProperty("downloadUrl")]
        public string DownloadUrl;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;
    }
}
