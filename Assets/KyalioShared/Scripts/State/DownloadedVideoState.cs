using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kyalio.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace Kyalio.State
{
    /// <summary>
    /// Local state for downloaded videos.
    /// Stored at Application.persistentDataPath/downloadedVideos.json.
    /// Follows the same pattern as UserLocalState; retained after logout.
    /// </summary>
    public class DownloadedVideoState
    {
        private static DownloadedVideoState _instance;
        public static DownloadedVideoState Instance => _instance ??= Load();

        private static readonly string FilePath =
            Path.Combine(Application.persistentDataPath, "downloadedVideos.json");

        public List<DownloadRecord> Records = new();

        /// <summary>
        /// Fires whenever a record is added or removed; UI components (e.g. PlaylistItemRow) subscribe to refresh their state.
        /// </summary>
        public static event Action OnRecordsChanged;

        /// <summary>
        /// Returns the absolute path to the local video file.
        /// If the file has been deleted, clears the record and returns null.
        /// </summary>
        public string GetFilePath(string projectId, string videoId)
        {
            var rec = Records.FirstOrDefault(r => r.ProjectId == projectId && r.VideoId == videoId);
            if (rec == null) return null;
            if (!File.Exists(rec.FilePath))
            {
                Records.Remove(rec);
                Save();
                return null;
            }
            return rec.FilePath;
        }

        public bool HasDownload(string projectId, string videoId) =>
            GetFilePath(projectId, videoId) != null;

        public void AddRecord(DownloadRecord record)
        {
            Records.RemoveAll(r => r.ProjectId == record.ProjectId && r.VideoId == record.VideoId);
            Records.Add(record);
            Save();
            OnRecordsChanged?.Invoke();
        }

        /// <summary>
        /// Removes the record and deletes the file from disk.
        /// </summary>
        public void RemoveRecord(string projectId, string videoId)
        {
            var rec = Records.FirstOrDefault(r => r.ProjectId == projectId && r.VideoId == videoId);
            if (rec == null) return;

            try
            {
                if (File.Exists(rec.FilePath))
                    File.Delete(rec.FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadedVideoState] Delete file failed: {e.Message}");
            }

            Records.Remove(rec);
            Save();
            OnRecordsChanged?.Invoke();
        }

        /// <summary>
        /// Removes all download records for a project and deletes the associated files from disk.
        /// </summary>
        public void RemoveAllForProject(string projectId)
        {
            var toDelete = Records.FindAll(r => r.ProjectId == projectId);
            if (toDelete.Count == 0) return;

            foreach (var rec in toDelete)
            {
                try
                {
                    if (File.Exists(rec.FilePath))
                        File.Delete(rec.FilePath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DownloadedVideoState] Delete file failed: {e.Message}");
                }
            }

            Records.RemoveAll(r => r.ProjectId == projectId);
            Save();
            OnRecordsChanged?.Invoke();
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadedVideoState] Save failed: {e.Message}");
            }
        }

        private static DownloadedVideoState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonConvert.DeserializeObject<DownloadedVideoState>(json)
                           ?? new DownloadedVideoState();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadedVideoState] Load failed: {e.Message}");
            }
            return new DownloadedVideoState();
        }

        public static void Reset() => _instance = new DownloadedVideoState();
    }
}
