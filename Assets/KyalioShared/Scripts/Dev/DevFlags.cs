namespace Kyalio.Dev
{
    /// <summary>
    /// Runtime flags for development and debug purposes.
    /// Set by DevBootstrapper at app startup; persists for the entire session.
    /// </summary>
    public static class DevFlags
    {
        public static bool UseFakeData { get; set; }
    }
}
