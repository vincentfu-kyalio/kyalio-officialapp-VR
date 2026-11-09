namespace Kyalio.Dev
{
    /// <summary>
    /// Runtime flags for development and debug purposes.
    /// Set by DevBootstrapper at app startup; persists for the entire session.
    /// </summary>
    public static class DevFlags
    {
        public static bool UseFakeData { get; set; }

        /// <summary>
        /// Video played by PlayVideoPage in fake-data mode when the episode has no
        /// local download. Relative to StreamingAssets, or an absolute path / URL.
        /// Empty = play nothing.
        /// </summary>
        public static string DevVideoPath { get; set; }
    }
}
