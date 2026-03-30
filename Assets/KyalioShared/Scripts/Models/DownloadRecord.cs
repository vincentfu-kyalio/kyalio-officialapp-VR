using System;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class DownloadRecord
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoId")]
        public string VideoId;

        [JsonProperty("filePath")]
        public string FilePath;

        [JsonProperty("downloadedAt")]
        public DateTime DownloadedAt;

        [JsonProperty("sizeBytes")]
        public long SizeBytes;
    }
}
