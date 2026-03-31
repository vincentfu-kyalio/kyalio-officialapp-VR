using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models;
using Kyalio.Repositories;
using Kyalio.Services;
using Kyalio.State;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// MyKyalio page: three horizontal project sections — Recently Watched, Downloads, Favorites.
    /// Inspector: recentlyWatchedSection, downloadsSection, favoritesSection
    /// </summary>
    public class MyKyalioPage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        [SerializeField] private RecentlyWatchedSection recentlyWatchedSection;
        [SerializeField] private TopicSection downloadsSection;
        [SerializeField] private TopicSection favoritesSection;

        private CancellationTokenSource _cts;
        private List<WatchHistoryProjectItem> _watchHistoryCache;
        private List<SubscribedProject> _favoritesCache;

        public void OnEnter(object param)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (downloadsSection != null)
                downloadsSection.OnSeeAllClicked = () => UIManager.Instance.GoTo(PageType.MyDownloads);
            if (favoritesSection != null)
                favoritesSection.OnSeeAllClicked = () => UIManager.Instance.GoTo(PageType.MyFavorites);

            if (DevFlags.UseFakeData) { LoadFakeData(); return; }

            LoadAsync(_cts.Token).Forget();
        }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData()
        {
            var fakeProjects = new System.Collections.Generic.List<SubscribedProject>
            {
                new SubscribedProject { Id = "p001", Name = "Heart Anatomy VR",            CategoryName = "Cardiology", PlaylistCount = 3 },
                new SubscribedProject { Id = "p003", Name = "Surgical Simulation Module 1",CategoryName = "Surgery",    PlaylistCount = 5 },
                new SubscribedProject { Id = "p005", Name = "Brain MRI Interpretation",    CategoryName = "Neurology",  PlaylistCount = 4 },
            };

            var fakeHistory = new System.Collections.Generic.List<WatchHistoryProjectItem>
            {
                new WatchHistoryProjectItem
                {
                    ProjectId    = "p001",
                    ProjectName  = "Heart Anatomy VR",
                    CategoryName = "Cardiology",
                    LatestEpisode = new WatchHistoryLatestEpisode { Title = "Introduction",  ProgressMs = 900_000,  DurationMs = 1_800_000, Ordinal = 1 },
                },
                new WatchHistoryProjectItem
                {
                    ProjectId    = "p003",
                    ProjectName  = "Surgical Simulation Module 1",
                    CategoryName = "Surgery",
                    LatestEpisode = new WatchHistoryLatestEpisode { Title = "Episode 1",     ProgressMs = 240_000,  DurationMs = 2_400_000, Ordinal = 1 },
                },
            };

            if (recentlyWatchedSection != null)
            {
                recentlyWatchedSection.gameObject.SetActive(true);
                recentlyWatchedSection.OnProjectClicked = projectId =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = projectId, Source = "direct" });
                recentlyWatchedSection.Bind("Recently Watched", fakeHistory);
            }

            if (downloadsSection != null)
            {
                downloadsSection.gameObject.SetActive(true);
                downloadsSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = p.Id, Source = "direct" });
                downloadsSection.Bind("Downloads", fakeProjects);
            }

            if (favoritesSection != null)
            {
                favoritesSection.gameObject.SetActive(true);
                favoritesSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = p.Id, Source = "favorites" });
                favoritesSection.Bind("Favorites", fakeProjects);
            }
        }

        public void OnExit()
        {
            _cts?.Cancel();
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            var state = AppState.Instance;
            bool needsWatch     = state.WatchHistoryDirty || _watchHistoryCache == null;
            bool needsFavorites = state.FavoritesDirty    || _favoritesCache    == null;

            // Restore from cache immediately for any section that doesn't need re-fetching
            RefreshDownloads();
            if (!needsWatch     && _watchHistoryCache != null) BindRecentlyWatched(_watchHistoryCache);
            if (!needsFavorites && _favoritesCache    != null) BindFavorites(_favoritesCache);

            if (!needsWatch && !needsFavorites) return;

            LoadingOverlay.Instance.Show();
            try
            {
                var tasks = new System.Collections.Generic.List<UniTask>();
                if (needsWatch)     tasks.Add(RefreshRecentlyWatchedAsync(ct));
                if (needsFavorites) tasks.Add(RefreshFavoritesAsync(ct));
                await UniTask.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[MyKyalioPage] Load failed: {e.Message}"); }
            finally { LoadingOverlay.Instance.Hide(); }
        }

        // ── Recently Watched ──────────────────────────────────────────

        private async UniTask RefreshRecentlyWatchedAsync(CancellationToken ct)
        {
            if (recentlyWatchedSection == null) return;

            // Render from cache immediately to avoid visual jump on GoBack
            if (_watchHistoryCache != null)
                BindRecentlyWatched(_watchHistoryCache);

            List<WatchHistoryProjectItem> fresh;
            try
            {
                var response = await ServiceLocator.Instance.WatchHistoryService
                    .GetProjectHistoryAsync(limit: 20, ct: ct);
                if (ct.IsCancellationRequested) return;
                fresh = response?.Items ?? new List<WatchHistoryProjectItem>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MyKyalioPage] Watch history fetch failed: {e.Message}");
                return;
            }

            // Re-bind only when the project order has changed
            if (!SameProjectOrder(_watchHistoryCache, fresh))
                BindRecentlyWatched(fresh);

            _watchHistoryCache = fresh;
            AppState.Instance.ClearWatchHistoryDirty();
        }

        private void BindRecentlyWatched(List<WatchHistoryProjectItem> items)
        {
            recentlyWatchedSection.gameObject.SetActive(items.Count > 0);
            if (items.Count > 0)
            {
                recentlyWatchedSection.OnProjectClicked = projectId =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = projectId, Source = "direct" });
                recentlyWatchedSection.Bind("Recently Watched", items);
            }
        }

        private static bool SameProjectOrder(
            List<WatchHistoryProjectItem> a, List<WatchHistoryProjectItem> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].ProjectId != b[i].ProjectId) return false;
            return true;
        }

        // ── Downloads ─────────────────────────────────────────────────

        private void RefreshDownloads()
        {
            if (downloadsSection == null) return;

            var projects = new List<SubscribedProject>();
            var seen     = new HashSet<string>();

            var sortedRecords = DownloadedVideoState.Instance.Records
                .OrderByDescending(r => r.DownloadedAt);

            foreach (var record in sortedRecords)
            {
                if (!seen.Add(record.ProjectId)) continue;
                var p = ProjectCacheRepository.Instance.AllProjects.Find(x => x.Id == record.ProjectId);
                if (p != null) projects.Add(p);
            }

            downloadsSection.gameObject.SetActive(projects.Count > 0);
            if (projects.Count > 0)
            {
                downloadsSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = p.Id, Source = "direct" });
                downloadsSection.Bind("Downloads", projects);
            }
        }

        // ── Favorites ─────────────────────────────────────────────────

        private async UniTask RefreshFavoritesAsync(CancellationToken ct)
        {
            if (favoritesSection == null) return;

            var response = await ServiceLocator.Instance.FavoriteService.GetFavoritesAsync(ct);
            if (ct.IsCancellationRequested) return;

            var projects = new List<SubscribedProject>();
            var items    = response?.Items;

            if (items != null)
            {
                foreach (var fav in items)
                {
                    if (fav.ProjectId == null) continue;
                    var p = ProjectCacheRepository.Instance.AllProjects.Find(x => x.Id == fav.ProjectId);
                    projects.Add(p ?? FavoriteItemToProject(fav));
                }
            }

            _favoritesCache = projects;
            AppState.Instance.ClearFavoritesDirty();
            BindFavorites(projects);
        }

        private void BindFavorites(List<SubscribedProject> projects)
        {
            if (favoritesSection == null) return;
            favoritesSection.gameObject.SetActive(projects.Count > 0);
            if (projects.Count > 0)
            {
                favoritesSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = p.Id, Source = "favorites" });
                favoritesSection.Bind("Favorites", projects);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static SubscribedProject FavoriteItemToProject(FavoriteItem fav) =>
            new SubscribedProject
            {
                Id                      = fav.ProjectId,
                Name                    = fav.ProjectName,
                CategoryName            = fav.CategoryName,
                ThumbnailUrl            = fav.ThumbnailUrl,
                ProgramPicUrl           = fav.ProgramPicUrl,
                PlaylistDurationSeconds = fav.PlaylistDurationSeconds,
                PlaylistCount           = fav.VideoCount
            };
    }
}
