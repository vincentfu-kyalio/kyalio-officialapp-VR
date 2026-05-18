using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    /// <summary>
    /// Unified credential schema. Mobile responses populate refreshToken+refreshTokenExpiresAt;
    /// Quest responses leave those null.
    /// </summary>
    public class Credential
    {
        [JsonProperty("accessToken")]
        public string AccessToken;

        [JsonProperty("tokenType")]
        public string TokenType;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;

        /// <summary>Mobile only; null for Quest.</summary>
        [JsonProperty("refreshToken")]
        public string RefreshToken;

        /// <summary>Mobile only; null for Quest.</summary>
        [JsonProperty("refreshTokenExpiresAt")]
        public string RefreshTokenExpiresAt;
    }
}
