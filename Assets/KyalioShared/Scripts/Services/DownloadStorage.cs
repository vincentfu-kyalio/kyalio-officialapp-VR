using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Kyalio.Services
{
    /// <summary>
    /// Where and how downloaded videos sit on disk. This is obfuscation, not DRM: it keeps a
    /// casual user from browsing to the files and playing or copying them, nothing more.
    ///
    /// On the Quest (Android player):
    ///   • Files live in the app's internal files dir (Context.getFilesDir), which neither
    ///     MTP nor `adb pull` can reach on a non-debuggable (release) build.
    ///   • File names are a hash of project/video ids with a neutral extension, so they do
    ///     not reveal the content or open in a player on a double-click.
    ///   • Each file starts with <see cref="HeaderSize"/> random bytes, so the MP4 boxes are
    ///     not where a player looks for them. AVPro skips them via its Android fileOffset.
    ///
    /// In the Editor files stay plain .mp4 without a header: the Windows players rely on the
    /// extension and AVPro's fileOffset is Android-only.
    /// </summary>
    public static class DownloadStorage
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        public const int HeaderSize = 1024;
        private const string Extension = ".kyv";
#else
        public const int HeaderSize = 0;
        private const string Extension = ".mp4";
#endif

        private const string FolderName = "media";

        /// <summary>Folder used before private storage; its contents are discarded on upgrade.</summary>
        public static string LegacyRoot => Path.Combine(Application.persistentDataPath, "downloads");

        private static string _root;

        /// <summary>Absolute folder holding the downloaded videos.</summary>
        public static string Root => _root ??= Path.Combine(ResolveBaseDir(), FolderName);

        /// <summary>The on-disk file name for a video (no folder).</summary>
        public static string GetFileName(string projectId, string videoId)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{projectId}_{videoId}"));
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
            return sb.Append(Extension).ToString();
        }

        /// <summary>Absolute path for a stored file name, or null when the name is empty.</summary>
        public static string Resolve(string fileName) =>
            string.IsNullOrEmpty(fileName) ? null : Path.Combine(Root, fileName);

        public static string GetFilePath(string projectId, string videoId) =>
            Resolve(GetFileName(projectId, videoId));

        public static void EnsureRoot()
        {
            if (!Directory.Exists(Root))
                Directory.CreateDirectory(Root);
        }

        /// <summary>Starts a fresh file containing only the random header (empty in the Editor).</summary>
        public static void WriteHeader(string path)
        {
            var header = new byte[HeaderSize];
            RandomNumberGenerator.Fill(header);
            File.WriteAllBytes(path, header);
        }

        private static string ResolveBaseDir()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Not disposed: Unity hands out a shared, cached activity reference.
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var dir = activity.Call<AndroidJavaObject>("getFilesDir");
                var path = dir.Call<string>("getAbsolutePath");
                if (!string.IsNullOrEmpty(path)) return path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadStorage] getFilesDir failed, using persistentDataPath: {e.Message}");
            }
#endif
            return Application.persistentDataPath;
        }
    }
}
