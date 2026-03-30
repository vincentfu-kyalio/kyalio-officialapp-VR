using System.Collections.Generic;
using System.Linq;
using Kyalio.Models;

namespace Kyalio.Repositories
{
    /// <summary>
    /// Client-side Project data cache with search/filter support. Pure C# singleton with no MonoBehaviour dependency.
    /// Build() is called by AppState.SetSubscriptions and lives for the entire session.
    /// </summary>
    public class ProjectCacheRepository
    {
        private static ProjectCacheRepository _instance;
        public static ProjectCacheRepository Instance => _instance ??= new ProjectCacheRepository();

        public List<SubscribedProject> AllProjects  { get; private set; } = new();
        public List<Category>          AllCategories { get; private set; } = new(); // specialties
        public List<Category>          AllPrograms   { get; private set; } = new(); // programs

        private readonly Dictionary<string, ProjectDetail> _detailCache = new();

        // ── Build ─────────────────────────────────────────────────────

        public void Build(List<SubscriptionItem> subscriptions)
        {
            var items = subscriptions ?? new List<SubscriptionItem>();

            AllProjects = items
                .SelectMany(s => s.Projects ?? new List<SubscribedProject>())
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            AllCategories = items
                .SelectMany(s => s.Categories ?? new List<Category>())
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            // Derive programs from AllProjects (no dedicated API list)
            AllPrograms = AllProjects
                .Where(p => !string.IsNullOrEmpty(p.ProgramId))
                .GroupBy(p => p.ProgramId)
                .Select(g => new Category { Id = g.Key, Name = g.First().ProgramName ?? g.Key })
                .OrderBy(c => c.Name)
                .ToList();
        }

        // ── Filter ────────────────────────────────────────────────────

        public List<SubscribedProject> Filter(FilterOptions options)
        {
            if (options == null || options.IsEmpty) return AllProjects;

            var results = AllProjects.AsEnumerable();

            if (options.CategoryIds != null && options.CategoryIds.Count > 0)
                results = results.Where(p => options.CategoryIds.Contains(p.CategoryId));

            if (options.ProgramIds != null && options.ProgramIds.Count > 0)
                results = results.Where(p => options.ProgramIds.Contains(p.ProgramId));

            if (!string.IsNullOrWhiteSpace(options.Query))
            {
                var q = options.Query.Trim();
                results = results.Where(p =>
                    Contains(p.Name, q) ||
                    Contains(p.DrName, q) ||
                    Contains(p.Institution, q) ||
                    Contains(p.Tag, q) ||
                    Contains(p.CategoryName, q) ||
                    Contains(p.RoleName, q));
            }

            return results.ToList();
        }

        // ── Detail Cache ──────────────────────────────────────────────

        public void CacheDetail(ProjectDetail detail)
        {
            if (detail?.Id != null)
                _detailCache[detail.Id] = detail;
        }

        public ProjectDetail GetCachedDetail(string projectId) =>
            _detailCache.TryGetValue(projectId, out var d) ? d : null;

        public int GetVideoCount(string projectId)
        {
            if (_detailCache.TryGetValue(projectId, out var d))
                return d.Playlist?.Count ?? d.PlaylistCount;
            var p = AllProjects.Find(x => x.Id == projectId);
            return p?.PlaylistCount ?? 0;
        }

        public int GetPlaylistDurationSeconds(string projectId)
        {
            if (_detailCache.TryGetValue(projectId, out var d))
                return d.PlaylistDurationSeconds;
            var p = AllProjects.Find(x => x.Id == projectId);
            return p?.PlaylistDurationSeconds ?? 0;
        }

        // ── Reset ─────────────────────────────────────────────────────

        public static void Reset() => _instance = new ProjectCacheRepository();

        // ── Helpers ───────────────────────────────────────────────────

        private static bool Contains(string source, string query) =>
            !string.IsNullOrEmpty(source) &&
            source.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
