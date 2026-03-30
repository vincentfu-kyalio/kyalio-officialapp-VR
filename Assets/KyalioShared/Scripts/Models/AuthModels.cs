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
}
