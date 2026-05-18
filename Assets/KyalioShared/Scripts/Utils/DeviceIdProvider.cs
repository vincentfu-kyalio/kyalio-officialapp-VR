using System;
using UnityEngine;

namespace Kyalio.Utils
{
    /// <summary>
    /// Stable per-install device ID for Quest pairing.
    /// The server stores only a hash; clients must keep the raw value across launches
    /// so the same physical Quest remains identifiable across pairing sessions.
    /// </summary>
    public static class DeviceIdProvider
    {
        private const string PrefsKey = "kyalio_device_id";

        public static string GetOrCreate()
        {
            var existing = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(existing)) return existing;

            var generated = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PrefsKey, generated);
            PlayerPrefs.Save();
            return generated;
        }
    }
}
