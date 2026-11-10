using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using UnityEngine;
using UnityEngine.UI;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Pages
{
    /// <summary>
    /// MyKyalio page: two-column layout.
    /// Left  — fixed sidebar: Recently Watched, Favorites, Downloads buttons.
    /// Right — panel swaps based on the selected tab.
    /// Inspector: recentlyWatchedButton, favoritesButton, downloadsButton,
    ///            recentlyWatchedPanel, favoritesPanel, downloadsPanel
    /// </summary>
    public class MyKyalioPage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        private enum Tab { RecentlyWatched, Favorites, Downloads }

        [Header("Left sidebar")]
        [SerializeField] private Button recentlyWatchedButton;
        [SerializeField] private Button favoritesButton;
        [SerializeField] private Button downloadsButton;

        [Header("Right panels")]
        [SerializeField] private RecentlyWatchedSection recentlyWatchedPanel;
        [SerializeField] private MyFavoritesPage favoritesPanel;
        [SerializeField] private MyFavoritesPage downloadsPanel;

        private Tab _activeTab;
        private MyFavoritesPage _activeListPanel;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            recentlyWatchedButton.onClick.AddListener(() => SelectTab(Tab.RecentlyWatched));
            favoritesButton.onClick.AddListener(() => SelectTab(Tab.Favorites));
            downloadsButton.onClick.AddListener(() => SelectTab(Tab.Downloads));
        }

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            // Fake data only covers Recently Watched here; Favorites / Downloads panels
            // handle fake mode in their own OnEnter.
            if (DevFlags.UseFakeData && _activeTab == Tab.RecentlyWatched) { LoadFakeData(); return; }

            // Return to the tab picked last (e.g. coming back from Project Info);
            // Recently Watched on first entry. Panels keep their scroll while hidden.
            SelectTab(_activeTab);
        }

        public void OnExit()
        {
            _cts?.Cancel();
            _activeListPanel?.OnExit();
            _activeListPanel = null;
        }

        // ── Tab switching ─────────────────────────────────────────────

        private void SelectTab(Tab tab)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _activeListPanel?.OnExit();
            _activeListPanel = null;

            _activeTab = tab;

            SelectedIndicator.Set(recentlyWatchedButton, tab == Tab.RecentlyWatched);
            SelectedIndicator.Set(favoritesButton,       tab == Tab.Favorites);
            SelectedIndicator.Set(downloadsButton,       tab == Tab.Downloads);

            if (recentlyWatchedPanel != null)
                recentlyWatchedPanel.gameObject.SetActive(tab == Tab.RecentlyWatched);
            if (favoritesPanel != null)
                favoritesPanel.gameObject.SetActive(tab == Tab.Favorites);
            if (downloadsPanel != null)
                downloadsPanel.gameObject.SetActive(tab == Tab.Downloads);

            switch (tab)
            {
                case Tab.RecentlyWatched:
                    LoadRecentlyWatchedAsync(_cts.Token).Forget();
                    break;
                case Tab.Favorites:
                    _activeListPanel = favoritesPanel;
                    favoritesPanel?.OnEnter(null);
                    break;
                case Tab.Downloads:
                    _activeListPanel = downloadsPanel;
                    downloadsPanel?.OnEnter(null);
                    break;
            }
        }

        // ── Recently Watched ──────────────────────────────────────────

        // Shows the list from AppState at once (usually prefetched after login), then
        // fetches again only if a playback marked it dirty. Joins the prefetch when it is
        // still running.
        private async UniTaskVoid LoadRecentlyWatchedAsync(CancellationToken ct)
        {
            var state  = AppState.Instance;
            var cached = state.WatchHistory;

            if (cached != null)
                BindRecentlyWatched(cached);

            if (cached != null && !state.WatchHistoryDirty) return;

            try
            {
                var fresh = await MyKyalioLoader.FetchWatchHistoryAsync().AttachExternalCancellation(ct);
                if (ct.IsCancellationRequested) return;

                if (!SameHistory(cached, fresh))
                    BindRecentlyWatched(fresh);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[MyKyalioPage] Watch history load failed: {e.Message}"); }
        }

        private void BindRecentlyWatched(List<WatchHistoryItem> items)
        {
            if (recentlyWatchedPanel == null) return;
            recentlyWatchedPanel.OnProjectClicked = projectId =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam { ProjectId = projectId, Source = ProjectPageSource.Direct });
            recentlyWatchedPanel.Bind("Recently Watched", items);
        }

        // Same cards in the same order, each on the same episode with the same progress.
        // Comparing projects alone missed a newer episode or progress on the top project.
        private static bool SameHistory(
            List<WatchHistoryItem> a, List<WatchHistoryItem> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].ProjectId  != b[i].ProjectId  ||
                    a[i].VideoId    != b[i].VideoId    ||
                    a[i].ProgressMs != b[i].ProgressMs ||
                    a[i].ProgressUpdatedAt != b[i].ProgressUpdatedAt)
                    return false;
            return true;
        }

        // ── Fake data ─────────────────────────────────────────────────

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData()
        {
            // Build recent items from the first few seeded projects' first videos.
            var fakeHistory = new List<WatchHistoryItem>();
            foreach (var p in ProjectCacheRepository.Instance.All.Take(2))
            {
                var firstVideo = p.Playlist != null && p.Playlist.Count > 0 ? p.Playlist[0] : null;
                if (firstVideo == null) continue;
                fakeHistory.Add(new WatchHistoryItem
                {
                    ProjectId  = p.ProjectId,
                    VideoId    = firstVideo.VideoId,
                    ProgressMs = firstVideo.DurationMs / 2,
                });
            }

            // Switch panel visuals without triggering async load
            _activeListPanel?.OnExit();
            _activeListPanel = null;

            _activeTab = Tab.RecentlyWatched;
            SelectedIndicator.Set(recentlyWatchedButton, true);
            SelectedIndicator.Set(favoritesButton,       false);
            SelectedIndicator.Set(downloadsButton,       false);
            if (recentlyWatchedPanel != null) recentlyWatchedPanel.gameObject.SetActive(true);
            if (favoritesPanel != null)       favoritesPanel.gameObject.SetActive(false);
            if (downloadsPanel != null)       downloadsPanel.gameObject.SetActive(false);

            BindRecentlyWatched(fakeHistory);
        }
    }
}
