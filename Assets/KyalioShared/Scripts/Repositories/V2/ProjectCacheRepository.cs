using System.Collections.Generic;
using System.Linq;
using Kyalio.Models.V2;

namespace Kyalio.Repositories.V2
{
    /// <summary>
    /// Client-side V2 cache. Populated by the new sync flow:
    ///   1. GET  /api/me/sync         → drives which projectIds to fetch / purge
    ///   2. POST /api/projects/batch  → call <see cref="ApplyBatch"/>
    ///   3. GET  /api/me/home         → call <see cref="ApplyHome"/> to overwrite filters
    ///   4. GET  /api/me/progress     → call <see cref="ApplyProgress"/>
    /// </summary>
    public class ProjectCacheRepository
    {
        private static ProjectCacheRepository _instance;
        public static ProjectCacheRepository Instance => _instance ??= new ProjectCacheRepository();

        // ── Stored data ──────────────────────────────────────────────

        private readonly Dictionary<string, Project> _projects = new();
        private readonly Dictionary<string, ProgressItem> _progressByVideoId = new();
        private readonly Dictionary<string, GrantedProjectVersion> _granted = new();

        public IReadOnlyDictionary<string, Project> Projects => _projects;
        public IReadOnlyDictionary<string, GrantedProjectVersion> Granted => _granted;
        public IReadOnlyDictionary<string, ProgressItem> ProgressByVideoId => _progressByVideoId;

        public List<IdNameRef> Specialties { get; private set; } = new();
        public List<ProgramSummary> Programs { get; private set; } = new();

        public string ThumbnailsExpireAt { get; private set; }
        public string ProgressTimestamp { get; private set; }
        public string SyncTimestamp { get; private set; }

        // ── Sync state ───────────────────────────────────────────────

        /// <summary>
        /// Records the authoritative granted set and returns the (missing, outdatedProject,
        /// outdatedThumbnail, removed) projectId groups so the caller can drive batch fetch
        /// and local-cache purge.
        /// </summary>
        public SyncDiff ApplySync(SyncResponse response)
        {
            var diff = new SyncDiff();
            var newGranted = response?.GrantedProjects ?? new Dictionary<string, GrantedProjectVersion>();

            foreach (var kv in newGranted)
            {
                var pid = kv.Key;
                var v   = kv.Value;
                if (!_projects.TryGetValue(pid, out var local))
                {
                    diff.Missing.Add(pid);
                    continue;
                }
                if (local.ProjectVersion != v.ProjectVersion)
                    diff.OutdatedProject.Add(pid);
                if (local.ThumbnailVersion != v.ThumbnailVersion)
                    diff.OutdatedThumbnail.Add(pid);
            }

            foreach (var pid in _projects.Keys)
                if (!newGranted.ContainsKey(pid))
                    diff.Removed.Add(pid);

            foreach (var pid in diff.Removed)
                _projects.Remove(pid);

            _granted.Clear();
            foreach (var kv in newGranted)
                _granted[kv.Key] = kv.Value;
            SyncTimestamp = response?.Timestamp;

            return diff;
        }

        public void ApplyBatch(ProjectsBatchResponse response)
        {
            if (response?.Items == null) return;
            foreach (var p in response.Items)
                if (!string.IsNullOrEmpty(p?.ProjectId))
                    _projects[p.ProjectId] = p;
            ThumbnailsExpireAt = response.ThumbnailsExpireAt;
        }

        public void ApplyProjectDetail(ProjectDetailResponse detail)
        {
            if (detail?.ProjectId == null) return;
            _projects[detail.ProjectId] = detail;
            if (!string.IsNullOrEmpty(detail.ThumbnailsExpireAt))
                ThumbnailsExpireAt = detail.ThumbnailsExpireAt;
        }

        public void ApplyHome(HomeResponse response)
        {
            Specialties = response?.Filters?.Specialties ?? new List<IdNameRef>();
            Programs    = response?.Filters?.Programs    ?? new List<ProgramSummary>();
            if (!string.IsNullOrEmpty(response?.ThumbnailsExpireAt))
                ThumbnailsExpireAt = response.ThumbnailsExpireAt;
        }

        public void ApplyProgress(ProgressResponse response, bool merge)
        {
            if (response?.Items == null) return;
            if (!merge) _progressByVideoId.Clear();
            foreach (var item in response.Items)
                if (!string.IsNullOrEmpty(item?.VideoId))
                    _progressByVideoId[item.VideoId] = item;
            ProgressTimestamp = response.Timestamp;
        }

        public void UpsertProgress(ProgressItem item)
        {
            if (!string.IsNullOrEmpty(item?.VideoId))
                _progressByVideoId[item.VideoId] = item;
        }

        // ── Lookups ──────────────────────────────────────────────────

        public Project Get(string projectId) =>
            _projects.TryGetValue(projectId, out var p) ? p : null;

        public IReadOnlyCollection<Project> All => _projects.Values;

        public string GetSpecialtyName(string specialtyId)
        {
            if (string.IsNullOrEmpty(specialtyId)) return null;
            return Specialties.FirstOrDefault(s => s.Id == specialtyId)?.Name;
        }

        public ProgramSummary GetProgram(string programId)
        {
            if (string.IsNullOrEmpty(programId)) return null;
            return Programs.FirstOrDefault(p => p.Id == programId);
        }

        public int GetProgressMs(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return 0;
            return _progressByVideoId.TryGetValue(videoId, out var p) ? p.ProgressMs : 0;
        }

        public string GetProgressUpdatedAt(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;
            return _progressByVideoId.TryGetValue(videoId, out var p) ? p.ProgressUpdatedAt : null;
        }

        public static void Reset() => _instance = new ProjectCacheRepository();
    }

    /// <summary>
    /// Result of comparing /api/me/sync against the local cache.
    /// Missing + OutdatedProject is what to send to POST /api/projects/batch;
    /// OutdatedThumbnail must invalidate local images before re-downloading;
    /// Removed must purge cached metadata + thumbnails + downloaded files.
    /// </summary>
    public class SyncDiff
    {
        public readonly List<string> Missing            = new();
        public readonly List<string> OutdatedProject    = new();
        public readonly List<string> OutdatedThumbnail  = new();
        public readonly List<string> Removed            = new();

        public IEnumerable<string> ToBatch =>
            new HashSet<string>(System.Linq.Enumerable.Concat(Missing, OutdatedProject));
    }
}
