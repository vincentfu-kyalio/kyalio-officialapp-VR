using System;
using Kyalio.Services;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class DownloadRecord
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoId")]
        public string VideoId;

        /// <summary>File name inside <see cref="DownloadStorage.Root"/>; the folder is resolved at runtime.</summary>
        [JsonProperty("fileName")]
        public string FileName;

        /// <summary>
        /// Absolute path written by builds before private storage. Only read so the upgrade
        /// can recognise and discard those records; never written by current builds.
        /// </summary>
        [JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
        public string LegacyFilePath;

        [JsonProperty("downloadedAt")]
        public DateTime DownloadedAt;

        [JsonProperty("sizeBytes")]
        public long SizeBytes;

        /// <summary>Absolute path of the local file, or null for a legacy record.</summary>
        [JsonIgnore]
        public string FilePath => DownloadStorage.Resolve(FileName);

        [JsonIgnore]
        public bool IsLegacy => string.IsNullOrEmpty(FileName);
    }

    /// <summary>A server-side DELETE /download still owed for a discarded local file.</summary>
    public class PendingServerDelete
    {
        [JsonProperty("projectId")]
        public string ProjectId;

        [JsonProperty("videoId")]
        public string VideoId;
    }
}
