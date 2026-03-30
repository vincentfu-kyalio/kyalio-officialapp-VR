namespace Kyalio.Utils
{
    public static class DurationFormatter
    {
        /// <summary>
        /// Converts milliseconds to "m:ss" or "h:mm:ss" format.
        /// </summary>
        public static string Format(int? durationMs)
        {
            if (durationMs == null || durationMs <= 0) return "--:--";

            var totalSeconds = durationMs.Value / 1000;
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;

            return hours > 0
                ? $"{hours}:{minutes:D2}:{seconds:D2}"
                : $"{minutes}:{seconds:D2}";
        }

        /// <summary>
        /// Converts milliseconds to "m:ss" or "h:mm:ss" format (long overload, used for playback progress).
        /// </summary>
        public static string Format(long ms)
        {
            if (ms <= 0) return "0:00";

            var totalSeconds = ms / 1000;
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;

            return hours > 0
                ? $"{hours}:{minutes:D2}:{seconds:D2}"
                : $"{minutes}:{seconds:D2}";
        }
    }
}
