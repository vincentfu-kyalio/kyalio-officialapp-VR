using System;
using System.Globalization;
using Kyalio.Models.V2;
using UnityEngine;

namespace Kyalio.Utils
{
    /// <summary>
    /// Persists login state across app launches (PlayerPrefs, app-private storage).
    ///
    ///   • Quest credential — accessToken + expiresAt. Restored on launch so the user skips
    ///     login while the token is still valid. Quest tokens are not refreshable; once
    ///     expiresAt passes (or the server revokes the session → 401) the user signs in again.
    ///   • Remember Me — toggle state, email and password for pre-filling the login form.
    ///
    /// Note: PlayerPrefs is plain text inside the app sandbox. Acceptable for a single-user
    /// headset, but it is not a secure vault.
    /// </summary>
    public static class AuthStorage
    {
        private const string TokenKey      = "kyalio_quest_access_token";
        private const string ExpiresAtKey  = "kyalio_quest_token_expires_at";
        private const string RememberKey   = "kyalio_remember_me";
        private const string EmailKey      = "kyalio_remember_email";
        private const string PasswordKey   = "kyalio_remember_password";

        /// <summary>Treat the token as expired this long before expiresAt.</summary>
        private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

        // ── Credential ───────────────────────────────────────────────

        public static void SaveCredential(Credential credential)
        {
            if (credential == null || string.IsNullOrEmpty(credential.AccessToken)) return;
            PlayerPrefs.SetString(TokenKey, credential.AccessToken);
            PlayerPrefs.SetString(ExpiresAtKey, credential.ExpiresAt ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the stored access token if it has not expired yet. An expired or
        /// unparseable entry is cleared.
        /// </summary>
        public static bool TryGetValidToken(out string token)
        {
            token = PlayerPrefs.GetString(TokenKey, "");
            if (string.IsNullOrEmpty(token)) return false;

            var expiresAtIso = PlayerPrefs.GetString(ExpiresAtKey, "");
            if (!DateTime.TryParse(expiresAtIso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var expiresAt)
                || expiresAt.ToUniversalTime() - ExpirySkew <= DateTime.UtcNow)
            {
                ClearCredential();
                token = null;
                return false;
            }
            return true;
        }

        public static void ClearCredential()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(ExpiresAtKey);
            PlayerPrefs.Save();
        }

        // ── Remember Me ──────────────────────────────────────────────

        /// <summary>
        /// Returns false when the user has never saved a preference (caller keeps the
        /// scene's default toggle state).
        /// </summary>
        public static bool TryLoadRemembered(out bool remember, out string email, out string password)
        {
            remember = PlayerPrefs.GetInt(RememberKey, 0) == 1;
            email    = PlayerPrefs.GetString(EmailKey, "");
            password = PlayerPrefs.GetString(PasswordKey, "");
            return PlayerPrefs.HasKey(RememberKey);
        }

        /// <summary>Stores email + password when remember is on; otherwise wipes them.</summary>
        public static void SaveRemembered(bool remember, string email, string password)
        {
            PlayerPrefs.SetInt(RememberKey, remember ? 1 : 0);
            if (remember)
            {
                PlayerPrefs.SetString(EmailKey, email ?? "");
                PlayerPrefs.SetString(PasswordKey, password ?? "");
            }
            else
            {
                PlayerPrefs.DeleteKey(EmailKey);
                PlayerPrefs.DeleteKey(PasswordKey);
            }
            PlayerPrefs.Save();
        }
    }
}
