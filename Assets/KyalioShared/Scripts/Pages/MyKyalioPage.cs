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
        private List<WatchHistoryItem> _watchHistoryCache;

        private void Awake()
        {
            recentlyWatchedButton.onClick.AddListener(() => SelectTab(Tab.RecentlyWatched));
            favoritesButton.onClick.AddListener(() => SelectTab(Tab.Favorites));
            downloadsButton.onClick.AddListener(() => SelectTab(Tab.Downloads));
        }

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            if (DevFlags.UseFakeData) { LoadFakeData(); return; }

            SelectTab(Tab.RecentlyWatched);
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

            SetSelectedVisual(recentlyWatchedButton, tab == Tab.RecentlyWatched);
            SetSelectedVisual(favoritesButton,       tab == Tab.Favorites);
            SetSelectedVisual(downloadsButton,       tab == Tab.Downloads);

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

        private static void SetSelectedVisual(Button button, bool selected)
        {
            var indicator = button.transform.Find("SelectedIndicator");
            if (indicator != null)
                indicator.gameObject.SetActive(selected);
        }

        // ── Recently Watched ──────────────────────────────────────────

        private async UniTaskVoid LoadRecentlyWatchedAsync(CancellationToken ct)
        {
            bool needsFresh = AppState.Instance.WatchHistoryDirty || _watchHistoryCache == null;

            if (_watchHistoryCache != null)
                BindRecentlyWatched(_watchHistoryCache);

            if (!needsFresh) return;

            try
            {
                var response = await ServiceLocator.Instance.V2.WatchHistory
                    .GetHistoryAsync(mode: WatchHistoryMode.Project, limit: 20, ct: ct);
                if (ct.IsCancellationRequested) return;

                var fresh = response?.Items ?? new List<WatchHistoryItem>();

                if (!SameProjectOrder(_watchHistoryCache, fresh))
                    BindRecentlyWatched(fresh);

                _watchHistoryCache = fresh;
                AppState.Instance.ClearWatchHistoryDirty();
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

        private static bool SameProjectOrder(
            List<WatchHistoryItem> a, List<WatchHistoryItem> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].ProjectId != b[i].ProjectId) return false;
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
            SetSelectedVisual(recentlyWatchedButton, true);
            SetSelectedVisual(favoritesButton,       false);
            SetSelectedVisual(downloadsButton,       false);
            if (recentlyWatchedPanel != null) recentlyWatchedPanel.gameObject.SetActive(true);
            if (favoritesPanel != null)       favoritesPanel.gameObject.SetActive(false);
            if (downloadsPanel != null)       downloadsPanel.gameObject.SetActive(false);

            BindRecentlyWatched(fakeHistory);
        }
    }
}
