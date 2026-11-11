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
    ///     The password is encrypted with an Android Keystore key (see SecureStore);
    ///     the email is stored as-is.
    ///
    /// Note: PlayerPrefs itself is plain text inside the app sandbox. The access token is
    /// short-lived and stored as-is.
    /// </summary>
    public static class AuthStorage
    {
        private const string TokenKey      = "kyalio_quest_access_token";
        private const string ExpiresAtKey  = "kyalio_quest_token_expires_at";
        private const string RememberKey   = "kyalio_remember_me";
        private const string EmailKey      = "kyalio_remember_email";
        private const string PasswordKey   = "kyalio_remember_password_enc";
        /// <summary>Plaintext password written by builds before SecureStore; migrated on load.</summary>
        private const string LegacyPasswordKey = "kyalio_remember_password";

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
        /// scene's default toggle state). A password that can never be decrypted (key wiped,
        /// data corrupted) is dropped; one that fails transiently is kept for the next
        /// launch. Either way it is returned as "".
        /// </summary>
        public static bool TryLoadRemembered(out bool remember, out string email, out string password)
        {
            MigrateLegacyPassword();

            remember = PlayerPrefs.GetInt(RememberKey, 0) == 1;
            email    = PlayerPrefs.GetString(EmailKey, "");
            password = "";

            var stored = PlayerPrefs.GetString(PasswordKey, "");
            if (!string.IsNullOrEmpty(stored))
            {
                switch (SecureStore.Unprotect(stored, out var plaintext))
                {
                    case SecureStore.UnprotectResult.Success:
                        password = plaintext;
                        break;
                    case SecureStore.UnprotectResult.Invalid:
                        ForgetPassword();
                        break;
                    // Unavailable: keep the ciphertext; it may decrypt on the next launch.
                }
            }
            return PlayerPrefs.HasKey(RememberKey);
        }

        /// <summary>Stores email + encrypted password when remember is on; otherwise wipes them.</summary>
        public static void SaveRemembered(bool remember, string email, string password)
        {
            PlayerPrefs.SetInt(RememberKey, remember ? 1 : 0);
            PlayerPrefs.DeleteKey(LegacyPasswordKey);
            if (remember)
            {
                PlayerPrefs.SetString(EmailKey, email ?? "");
                var protectedPassword = string.IsNullOrEmpty(password) ? null : SecureStore.Protect(password);
                // Never fall back to plaintext: if encryption fails, just don't remember the password.
                if (protectedPassword != null) PlayerPrefs.SetString(PasswordKey, protectedPassword);
                else PlayerPrefs.DeleteKey(PasswordKey);
            }
            else
            {
                PlayerPrefs.DeleteKey(EmailKey);
                PlayerPrefs.DeleteKey(PasswordKey);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Drops the remembered password but keeps the email and toggle — used when the
        /// server rejects it (e.g. the password was changed elsewhere).
        /// </summary>
        public static void ForgetPassword()
        {
            if (!PlayerPrefs.HasKey(PasswordKey) && !PlayerPrefs.HasKey(LegacyPasswordKey)) return;
            PlayerPrefs.DeleteKey(PasswordKey);
            PlayerPrefs.DeleteKey(LegacyPasswordKey);
            PlayerPrefs.Save();
        }

        /// <summary>Re-saves a plaintext password left by an older build in encrypted form.</summary>
        private static void MigrateLegacyPassword()
        {
            if (!PlayerPrefs.HasKey(LegacyPasswordKey)) return;

            var legacy = PlayerPrefs.GetString(LegacyPasswordKey, "");
            PlayerPrefs.DeleteKey(LegacyPasswordKey);

            var protectedPassword = PlayerPrefs.GetInt(RememberKey, 0) == 1 && !string.IsNullOrEmpty(legacy)
                ? SecureStore.Protect(legacy)
                : null;
            if (protectedPassword != null) PlayerPrefs.SetString(PasswordKey, protectedPassword);
            PlayerPrefs.Save();
        }
    }
}
