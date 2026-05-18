using Newtonsoft.Json;

namespace Kyalio.Models.V2
{
    // ── App version check ────────────────────────────────────────────

    public class AppVersionCheckResponse
    {
        [JsonProperty("deviceType")]
        public string DeviceType;

        [JsonProperty("currentVersion")]
        public string CurrentVersion;

        [JsonProperty("minimumVersion")]
        public string MinimumVersion;

        [JsonProperty("updateRequired")]
        public bool UpdateRequired;

        [JsonProperty("storeUrl")]
        public string StoreUrl;
    }

    // ── Mobile login ─────────────────────────────────────────────────

    public class LoginRequest
    {
        [JsonProperty("email")]
        public string Email;

        [JsonProperty("password")]
        public string Password;

        [JsonProperty("deviceId")]
        public string DeviceId;

        /// <summary>"phone" or "tablet".</summary>
        [JsonProperty("deviceKind")]
        public string DeviceKind;

        [JsonProperty("deviceModelCode")]
        public string DeviceModelCode;

        [JsonProperty("deviceOs")]
        public string DeviceOs;
    }

    public class CredentialResponse
    {
        [JsonProperty("credential")]
        public Credential Credential;
    }

    // ── Device conflict during login / device switch ─────────────────

    public class DeviceSummary
    {
        [JsonProperty("deviceModelCode")]
        public string DeviceModelCode;

        [JsonProperty("deviceOs")]
        public string DeviceOs;

        [JsonProperty("lastSeenAt")]
        public string LastSeenAt;
    }

    /// <summary>
    /// 409 body for DEVICE_REPLACE_REQUIRED / DEVICE_ACCOUNT_SWITCH_REQUIRED /
    /// DEVICE_REPLACE_SWITCH_REQUIRED. ticketType is "replace_switch" only for
    /// the combined case.
    /// </summary>
    public class DeviceConflictResponse
    {
        [JsonProperty("error")]
        public ErrorEnvelope Error;

        [JsonProperty("ticket")]
        public string Ticket;

        [JsonProperty("ticketType")]
        public string TicketType;

        [JsonProperty("deviceKind")]
        public string DeviceKind;

        [JsonProperty("existingDevice")]
        public DeviceSummary ExistingDevice;

        [JsonProperty("replaceDevice")]
        public DeviceSummary ReplaceDevice;

        [JsonProperty("switchDevice")]
        public DeviceSummary SwitchDevice;
    }

    public class DeviceTicketRequest
    {
        [JsonProperty("ticket")]
        public string Ticket;
    }

    // ── Refresh / logout ─────────────────────────────────────────────

    public class RefreshRequest
    {
        [JsonProperty("refreshToken")]
        public string RefreshToken;
    }

    public class LogoutRequest
    {
        [JsonProperty("refreshToken")]
        public string RefreshToken;
    }

    // ── Forgot password ──────────────────────────────────────────────

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

    // ── Quest pairing ────────────────────────────────────────────────

    /// <summary>
    /// Request body for POST /api/pair/request.
    /// deviceId is REQUIRED — a stable per-Quest UUID (the server stores only its hash).
    /// </summary>
    public class PairRequest
    {
        [JsonProperty("deviceId")]
        public string DeviceId;

        [JsonProperty("model")]
        public string Model;

        [JsonProperty("deviceOs")]
        public string DeviceOs;
    }

    public class PairRequestResponse
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("expiresAt")]
        public string ExpiresAt;
    }

    /// <summary>
    /// Payload carried by the SSE "verified" event on GET /api/pair/stream/{code}.
    /// </summary>
    public class PairStreamPayload
    {
        [JsonProperty("status")]
        public string Status;

        [JsonProperty("credential")]
        public Credential Credential;
    }

    /// <summary>
    /// Body for POST /api/pair/verify (called from the mobile app).
    /// </summary>
    public class PairVerifyRequest
    {
        [JsonProperty("code")]
        public string Code;
    }
}
