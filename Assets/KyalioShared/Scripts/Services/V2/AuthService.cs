using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using Kyalio.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// V2 auth surface. For the Quest build this covers pairing (request + stream + verify
    /// from the mobile side is N/A here) and logout. For the mobile build it also covers
    /// login / refresh / forgot password / device replace+switch / version check.
    /// </summary>
    public class AuthService
    {
        private readonly ApiClient _client;
        private readonly IReadOnlyDictionary<string, string> _appKeyHeader;
        private readonly IReadOnlyDictionary<string, string> _questKeyHeader;

        public AuthService(ApiClient client, string appKey, string questKey)
        {
            _client = client;
            _appKeyHeader = string.IsNullOrEmpty(appKey)
                ? null
                : new Dictionary<string, string> { ["X-App-Key"] = appKey };
            _questKeyHeader = string.IsNullOrEmpty(questKey)
                ? null
                : new Dictionary<string, string> { ["X-Quest-Key"] = questKey };
        }

        // ── App version check ────────────────────────────────────────

        /// <summary>GET /api/app/version-check (no Bearer).</summary>
        public UniTask<AppVersionCheckResponse> CheckVersionAsync(CancellationToken ct = default)
        {
            var headers = _questKeyHeader ?? _appKeyHeader;
            return _client.GetAsync<AppVersionCheckResponse>("/api/app/version-check", headers, ct);
        }

        // ── Quest pairing ────────────────────────────────────────────

        /// <summary>POST /api/pair/request (X-Quest-Key).</summary>
        public UniTask<PairRequestResponse> RequestPairAsync(CancellationToken ct = default)
        {
            var body = new PairRequest
            {
                DeviceId = DeviceIdProvider.GetOrCreate(),
                Model    = SystemInfo.deviceModel,
                DeviceOs = "Quest OS",
            };
            return _client.PostAsync<PairRequestResponse>(
                "/api/pair/request", body, _questKeyHeader, ct);
        }

        /// <summary>
        /// GET /api/pair/stream/{code} (X-Quest-Key, SSE).
        /// Returns the PairStreamPayload on "verified" or { Status = "expired" } on expiry.
        /// </summary>
        public UniTask<PairStreamPayload> StreamPairAsync(string code, CancellationToken ct = default)
            => _client.SseAsync<PairStreamPayload>(
                $"/api/pair/stream/{code}",
                _questKeyHeader,
                (eventName, data) => eventName switch
                {
                    "verified" => JsonConvert.DeserializeObject<PairStreamPayload>(data),
                    "expired"  => new PairStreamPayload { Status = "expired" },
                    _          => null,
                },
                ct);
        // ── Quest password login ─────────────────────────────────────

        /// <summary>
        /// POST /api/auth/quest/login (X-Quest-Key). Returns the same non-refreshable Quest
        /// credential as pairing. Throws ApiException 409 with a DeviceConflictResponse body
        /// when signing in would end another Quest session — confirm via
        /// <see cref="ConfirmQuestLoginAsync"/>.
        /// </summary>
        public UniTask<CredentialResponse> QuestLoginAsync(
            string email, string password, CancellationToken ct = default)
        {
            var body = new QuestLoginRequest
            {
                Email    = email,
                Password = password,
                DeviceId = DeviceIdProvider.GetOrCreate(),
                Model    = SystemInfo.deviceModel,
                DeviceOs = "Quest OS",
            };
            return _client.PostAsync<CredentialResponse>(
                "/api/auth/quest/login", body, _questKeyHeader, ct);
        }

        /// <summary>
        /// POST /api/auth/quest/login/confirm (X-Quest-Key). May throw a fresh 409 with a new
        /// ticket if another conflict appeared after the first ticket was issued.
        /// </summary>
        public UniTask<CredentialResponse> ConfirmQuestLoginAsync(
            string ticket, CancellationToken ct = default)
            => _client.PostAsync<CredentialResponse>(
                "/api/auth/quest/login/confirm",
                new DeviceTicketRequest { Ticket = ticket }, _questKeyHeader, ct);

        /// <summary>
        /// POST /api/pair/verify — confirm a 6-digit pairing code. Returns 204 on success;
        /// throws ApiException 404 (unknown/expired) or 409 (already used).
        /// </summary>
        public UniTask VerifyPairAsync(string code, CancellationToken ct = default)
            => _client.PostBodyAsync("/api/pair/verify", new PairVerifyRequest { Code = code }, ct);

        // ── Mobile login / refresh / devices / forgot ────────────────
        // (Kept here for parity with the mobile build; the Quest app does not call them.)

        public UniTask<CredentialResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
            => _client.PostAsync<CredentialResponse>("/api/auth/login", request, _appKeyHeader, ct);

        public UniTask<CredentialResponse> ReplaceDeviceAsync(string ticket, CancellationToken ct = default)
            => _client.PostAsync<CredentialResponse>(
                "/api/auth/devices/replace", new DeviceTicketRequest { Ticket = ticket }, _appKeyHeader, ct);

        public UniTask<CredentialResponse> SwitchDeviceAsync(string ticket, CancellationToken ct = default)
            => _client.PostAsync<CredentialResponse>(
                "/api/auth/devices/switch", new DeviceTicketRequest { Ticket = ticket }, _appKeyHeader, ct);

        public UniTask<CredentialResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
            => _client.PostAsync<CredentialResponse>(
                "/api/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, null, ct);

        /// <summary>
        /// POST /api/auth/forgot-password. Accepts X-Quest-Key or X-App-Key; the Quest build
        /// only has the Quest key, so prefer it.
        /// </summary>
        public UniTask<ForgotPasswordResponse> ForgotPasswordAsync(string email, CancellationToken ct = default)
            => _client.PostAsync<ForgotPasswordResponse>(
                "/api/auth/forgot-password", new ForgotPasswordRequest { Email = email },
                _questKeyHeader ?? _appKeyHeader, ct);

        // ── Logout ───────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/logout — revokes the supplied refresh token (mobile) or the active
        /// Quest session bound to the current Bearer (Quest). Always returns 204; safe to fire
        /// and forget. Quest callers pass null for refreshToken; the Bearer header is enough.
        /// </summary>
        public UniTask LogoutAsync(string refreshToken = null, CancellationToken ct = default)
            => _client.PostBodyAsync(
                "/api/auth/logout", new LogoutRequest { RefreshToken = refreshToken }, ct);

        // ── Current account ──────────────────────────────────────────

        /// <summary>
        /// GET /api/me — the signed-in member's email and display name. Login and pairing
        /// responses do not include the account, so call this after signing in.
        /// </summary>
        public UniTask<MeResponse> GetMeAsync(CancellationToken ct = default)
            => _client.GetAsync<MeResponse>("/api/me", ct);
    }
}
