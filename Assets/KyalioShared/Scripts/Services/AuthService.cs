using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using UnityEngine;

namespace Kyalio.Services
{
    /// <summary>
    /// Handles authentication via POST /api/auth/login and POST /api/auth/forgot-password.
    /// On successful login, injects the JWT token into ApiClient for all subsequent requests.
    /// Both auth endpoints require X-App-Key header (API v0.4.0+).
    /// </summary>
    public class AuthService
    {
        private readonly ApiClient _client;
        private readonly IReadOnlyDictionary<string, string> _appKeyHeader;

        private const string KeyToken         = "session_token";
        private const string KeyExpiresAt     = "session_expires_at";
        private const string KeyRememberEmail = "remember_email";
        private const string KeyRemember      = "remember_me";

        public AuthService(ApiClient client, string appKey)
        {
            _client = client;
            _appKeyHeader = new Dictionary<string, string> { ["X-App-Key"] = appKey };
        }

        // ── Login ─────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/login
        /// On success, stores the Bearer token in ApiClient.
        /// Throws ApiException: 401 = invalid credentials, 403 = bad app key, 429 = rate limited.
        /// </summary>
        public async UniTask<LoginResponse> LoginAsync(
            string email, string password, CancellationToken ct = default)
        {
            var response = await _client.PostAsync<LoginResponse>(
                "/api/auth/login",
                new LoginRequest { Email = email, Password = password },
                _appKeyHeader, ct);

            _client.SetToken(response.Credential.Token);
            return response;
        }

        // ── Subscriptions ─────────────────────────────────────────────

        /// <summary>
        /// GET /api/subscriptions
        /// Call immediately after login or session restore to retrieve the user's subscription packages.
        /// Requires Bearer token to be set (call LoginAsync or TryRestoreSession first).
        /// </summary>
        public UniTask<SubscriptionResolveResponse> GetSubscriptionsAsync(
            CancellationToken ct = default)
        {
            return _client.GetAsync<SubscriptionResolveResponse>("/api/subscriptions", ct);
        }

        // ── Session Persistence ───────────────────────────────────────

        /// <summary>
        /// Saves token, expiry, and email to PlayerPrefs.
        /// Call after a successful login when Remember Me is on.
        /// </summary>
        public void SaveSession(LoginResponse response, string email)
        {
            PlayerPrefs.SetInt(KeyRemember, 1);
            PlayerPrefs.SetString(KeyToken, response.Credential.Token);
            PlayerPrefs.SetString(KeyExpiresAt, response.Credential.ExpiresAt);
            PlayerPrefs.SetString(KeyRememberEmail, email);

            // Clean up keys from previous app versions
            PlayerPrefs.DeleteKey("remember_password");
            PlayerPrefs.DeleteKey("session_subscriptions");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears all session data from PlayerPrefs.
        /// </summary>
        public void ClearSession()
        {
            PlayerPrefs.SetInt(KeyRemember, 0);
            PlayerPrefs.DeleteKey(KeyToken);
            PlayerPrefs.DeleteKey(KeyExpiresAt);
            PlayerPrefs.DeleteKey(KeyRememberEmail);
            // Clean up keys from previous app versions
            PlayerPrefs.DeleteKey("remember_password");
            PlayerPrefs.DeleteKey("session_subscriptions");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Attempts to restore a previous session from PlayerPrefs.
        /// Returns the restored LoginResponse (with token) if the stored token is valid and not expired; returns null otherwise.
        /// After a successful restore, call GetSubscriptionsAsync() to retrieve fresh subscription data.
        /// </summary>
        public LoginResponse TryRestoreSession()
        {
            if (PlayerPrefs.GetInt(KeyRemember, 0) != 1) return null;

            var token     = PlayerPrefs.GetString(KeyToken, "");
            var expiresAt = PlayerPrefs.GetString(KeyExpiresAt, "");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expiresAt)) return null;

            if (!DateTime.TryParse(expiresAt, null,
                    DateTimeStyles.RoundtripKind, out var expiry)) return null;

            if (DateTime.UtcNow >= expiry.ToUniversalTime())
            {
                Debug.Log("[AuthService] Stored token expired — clearing session.");
                ClearSession();
                return null;
            }

            _client.SetToken(token);

            return new LoginResponse
            {
                Credential = new LoginCredential { Token = token, ExpiresAt = expiresAt }
            };
        }

        /// <summary>
        /// Returns true if a Remember-Me session is stored and the token has not yet expired.
        /// Pure read — does not set the token or modify any state.
        /// Safe to call synchronously from Awake().
        /// </summary>
        public bool HasStoredValidSession()
        {
            if (PlayerPrefs.GetInt(KeyRemember, 0) != 1) return false;
            var token     = PlayerPrefs.GetString(KeyToken, "");
            var expiresAt = PlayerPrefs.GetString(KeyExpiresAt, "");
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expiresAt)) return false;
            if (!DateTime.TryParse(expiresAt, null,
                    DateTimeStyles.RoundtripKind, out var expiry)) return false;
            return DateTime.UtcNow < expiry.ToUniversalTime();
        }

        /// <summary>Returns the remembered email for pre-filling the login form.</summary>
        public string GetRememberedEmail() =>
            PlayerPrefs.GetString(KeyRememberEmail, "");

        // ── Forgot Password ───────────────────────────────────────────

        /// <summary>
        /// POST /api/auth/forgot-password
        /// Always returns 200 (server prevents account enumeration).
        /// Throws ApiException: 403 = bad app key, 429 = rate limited.
        /// </summary>
        public UniTask<ForgotPasswordResponse> ForgotPasswordAsync(
            string email, CancellationToken ct = default)
        {
            return _client.PostAsync<ForgotPasswordResponse>(
                "/api/auth/forgot-password",
                new ForgotPasswordRequest { Email = email },
                _appKeyHeader, ct);
        }

        // ── Pair Verify ───────────────────────────────────────────────

        /// <summary>
        /// POST /api/pair/verify — Mobile App submits a 6-digit code to pair with a Quest.
        /// Requires Bearer token. Returns 204 on success.
        /// Throws ApiException: 404 = code not found/expired, 409 = code already used.
        /// </summary>
        public UniTask VerifyPairCodeAsync(string code, CancellationToken ct = default)
            => _client.PostBodyAsync("/api/pair/verify", new PairVerifyRequest { Code = code }, ct);

        // ── Logout ────────────────────────────────────────────────────

        public void Logout()
        {
            _client.ClearToken();
            ClearSession();
        }
    }
}
