using Kyalio.Models;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class LoginRequest
    {
        [JsonProperty("email")]
        public string Email;

        [JsonProperty("password")]
        public string Password;
    }

    public class ForgotPasswordRequest
    {
        [JsonProperty("email")]
        public string Email;
    }

    public class ForgotPasswordResponse
    {
        [JsonProperty("message")]
        public string Message;
    }

    public class LoginCredential
    {
        [JsonProperty("token")]
        public string Token;

        [JsonProperty("tokenType")]
        public string TokenType;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;
    }

    public class LoginResponse
    {
        [JsonProperty("credential")]
        public LoginCredential Credential;
    }

    public class PairVerifyRequest
    {
        [JsonProperty("code")]
        public string Code;
    }

    public class PairRequestBody
    {
        [JsonProperty("model")]    public string Model;
        [JsonProperty("serial")]   public string Serial;
        [JsonProperty("appVersion")] public string AppVersion;
    }

    public class PairRequestResponse
    {
        [JsonProperty("code")]      public string Code;
        [JsonProperty("expiresAt")] public string ExpiresAt;
    }

    public class PairPollCredential
    {
        [JsonProperty("token")]     public string Token;
        [JsonProperty("tokenType")] public string TokenType;
        [JsonProperty("expiresAt")] public string ExpiresAt;
    }

    public class PairPollResponse
    {
        [JsonProperty("status")]     public string Status;
        [JsonProperty("credential")] public PairPollCredential Credential;
    }
}
