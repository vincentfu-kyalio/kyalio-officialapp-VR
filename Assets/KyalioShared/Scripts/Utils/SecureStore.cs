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
    ///
    /// Both methods return null on failure; callers treat that as "nothing stored".
    /// </summary>
    public static class SecureStore
    {
        private const string KeystorePrefix = "ks1:";
        private const string PlainPrefix    = "plain:";

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string JavaClass = "com.kyalio.secure.SecureStore";

        public static string Protect(string plaintext)
        {
            if (plaintext == null) return null;
            try
            {
                using var cls = new AndroidJavaClass(JavaClass);
                var payload = cls.CallStatic<string>("encrypt", plaintext);
                return string.IsNullOrEmpty(payload) ? null : KeystorePrefix + payload;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SecureStore] Protect failed: {e.GetType().Name}");
                return null;
            }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(KeystorePrefix)) return null;
            try
            {
                using var cls = new AndroidJavaClass(JavaClass);
                return cls.CallStatic<string>("decrypt", stored.Substring(KeystorePrefix.Length));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SecureStore] Unprotect failed: {e.GetType().Name}");
                return null;
            }
        }
#else
        public static string Protect(string plaintext)
            => plaintext == null ? null : PlainPrefix + plaintext;

        public static string Unprotect(string stored)
            => !string.IsNullOrEmpty(stored) && stored.StartsWith(PlainPrefix)
                ? stored.Substring(PlainPrefix.Length)
                : null;
#endif
    }
}
