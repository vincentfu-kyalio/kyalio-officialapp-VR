using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Kyalio.State
{
    /// <summary>
    /// Locally persisted user data: owner email and Favourites.
    /// Stored at Application.persistentDataPath/userLocalState.json.
    /// </summary>
    public class UserLocalState
    {
        private static UserLocalState _instance;
        public static UserLocalState Instance => _instance ??= Load();

        private static readonly string FilePath =
            Path.Combine(Application.persistentDataPath, "userLocalState.json");

        // --- Data ---
        public string OwnerEmail = "";
        public List<string> FavoriteProjectIds = new();

        // ── Owner ─────────────────────────────────────────────────────

        /// <summary>
        /// Sets the owner email and persists to disk.
        /// </summary>
        public void SetOwner(string email)
        {
            OwnerEmail = email;
            Save();
        }

        // ── Favorites ────────────────────────────────────────────────

        public bool IsFavorite(string projectId) =>
            FavoriteProjectIds.Contains(projectId);

        public void AddFavorite(string projectId)
        {
            if (!FavoriteProjectIds.Contains(projectId))
            {
                FavoriteProjectIds.Add(projectId);
                Save();
            }
        }

        public void RemoveFavorites(IEnumerable<string> projectIds)
        {
            var ids = new HashSet<string>(projectIds);
            FavoriteProjectIds.RemoveAll(ids.Contains);
            Save();
        }

        // ── Persistence ───────────────────────────────────────────────

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UserLocalState] Save failed: {e.Message}");
            }
        }

        private static UserLocalState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonConvert.DeserializeObject<UserLocalState>(json) ?? new UserLocalState();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UserLocalState] Load failed: {e.Message}");
            }
            return new UserLocalState();
        }

        /// <summary>
        /// Resets state on logout (does not delete the local file; only clears in-memory data).
        /// </summary>
        public static void Reset() => _instance = new UserLocalState();
    }

}
