using UnityEngine;

namespace Kyalio.Utils
{
    /// <summary>
    /// Returns the device type string expected by the API.
    /// </summary>
    public static class DeviceTypeHelper
    {
        public static string Get()
        {
            return Application.platform switch
            {
                RuntimePlatform.IPhonePlayer => "ios",
                RuntimePlatform.Android      => "android",
                _                            => "mobile",
            };
        }
    }
}
