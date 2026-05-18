using Newtonsoft.Json;

namespace Kyalio.Models
{
    /// <summary>
    /// 426 APP_VERSION_UNSUPPORTED response body shared by all non-admin endpoints.
    /// Returned from <see cref="ErrorEnvelope"/> + version metadata at the response root.
    /// </summary>
    public class ForceUpdateInfo
    {
        [JsonProperty("error")]
        public ErrorEnvelope Error;

        [JsonProperty("deviceType")]
        public string DeviceType;

        [JsonProperty("currentVersion")]
        public string CurrentVersion;

        [JsonProperty("minimumVersion")]
        public string MinimumVersion;

        [JsonProperty("storeUrl")]
        public string StoreUrl;
    }

    public class ErrorEnvelope
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("message")]
        public string Message;
    }
}
