using System;
using UnityEngine;

namespace Kyalio.Utils
{
    /// <summary>
    /// Encrypts small secrets for local persistence.
    ///
    ///   • Android device — AES-256-GCM with a non-exportable Android Keystore key
    ///     (Assets/Plugins/Android/SecureStore.java). Ciphertext copied off the device
    ///     cannot be decrypted.
    ///   • Editor / other platforms — no keystore available; the value is stored with a
    ///     marker prefix but NOT encrypted. Development convenience only.
    /// </summary>
    public static class SecureStore
    {
        public enum UnprotectResult
        {
            Success,
            /// <summary>Can never be decrypted (key gone, data corrupt) — discard it.</summary>
            Invalid,
            /// <summary>Possibly transient failure — keep the stored value and retry later.</summary>
            Unavailable,
        }

        private const string KeystorePrefix = "ks1:";
        private const string PlainPrefix    = "plain:";

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string JavaClass = "com.kyalio.secure.SecureStore";

        // Cached for the app's lifetime; avoids a JNI FindClass + global ref per call.
        private static AndroidJavaClass _javaClass;
        private static AndroidJavaClass Java => _javaClass ??= new AndroidJavaClass(JavaClass);

        /// <summary>Returns the value to persist, or null on failure.</summary>
        public static string Protect(string plaintext)
        {
            if (plaintext == null) return null;
            try
            {
                var payload = Java.CallStatic<string>("encrypt", plaintext);
                return string.IsNullOrEmpty(payload) ? null : KeystorePrefix + payload;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SecureStore] Protect failed: {e.GetType().Name}");
                return null;
            }
        }

        public static UnprotectResult Unprotect(string stored, out string plaintext)
        {
            plaintext = null;
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(KeystorePrefix, StringComparison.Ordinal))
                return UnprotectResult.Invalid;

            string result;
            try
            {
                result = Java.CallStatic<string>("decrypt", stored.Substring(KeystorePrefix.Length));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SecureStore] Unprotect failed: {e.GetType().Name}");
                return UnprotectResult.Unavailable;
            }

            // Java contract: "1"+plaintext = ok, "0" = permanently invalid, null = transient.
            if (result == null) return UnprotectResult.Unavailable;
            if (result.Length == 0 || result[0] != '1') return UnprotectResult.Invalid;
            plaintext = result.Substring(1);
            return UnprotectResult.Success;
        }
#else
        public static string Protect(string plaintext)
            => plaintext == null ? null : PlainPrefix + plaintext;

        public static UnprotectResult Unprotect(string stored, out string plaintext)
        {
            plaintext = null;
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(PlainPrefix, StringComparison.Ordinal))
                return UnprotectResult.Invalid;
            plaintext = stored.Substring(PlainPrefix.Length);
            return UnprotectResult.Success;
        }
#endif
    }
}
