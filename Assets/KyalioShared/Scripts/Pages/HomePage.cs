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
using TMPro;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Home page: two-column layout.
    /// Left  — HomeMenuPanel sidebar: Latest Releases, Recommended, Categories.
    /// Right — ProjectCardList showing the selected category's projects.
    /// Selecting a different category cancels the in-flight load and starts a new one.
    /// Inspector: menuPanel, rightPanelTitle, projectList, loadingIndicator (optional)
    /// </summary>
    public class HomePage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        [SerializeField] private HomeMenuPanel menuPanel;
        [SerializeField] private TextMeshProUGUI rightPanelTitle;
        [SerializeField] private ProjectCardList projectList;
        [SerializeField] private GameObject loadingIndicator;

        private CancellationTokenSource _cts;

        // ── Fake data ─────────────────────────────────────────────────
        private List<SubscribedProject> _fakeLatest;
        private List<SubscribedProject> _fakeRecommended;
        private Dictionary<string, List<SubscribedProject>> _fakeCategoryMap;

        private void Awake()
        {
            if (menuPanel != null)
                menuPanel.OnSelectionChanged += OnMenuSelectionChanged;
        }

        public void OnEnter(object param)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (DevFlags.UseFakeData) { LoadFakeData(); return; }

            if (menuPanel != null)
                menuPanel.BuildCategories(ProjectCacheRepository.Instance.AllCategories);

            // Always start on Latest Releases when entering the page
            var initial = new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases };
            LoadSelectionAsync(initial, _cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
            projectList.Clear();
        }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData()
        {
            // FakeDataSeeder.Seed() has already populated ProjectCacheRepository.
            // Just read from it to build the section distribution.
            var allProjects = ProjectCacheRepository.Instance.AllProjects;

            _fakeLatest = allProjects
                .Where((_, i) => i % 3 == 0).Take(20).ToList();

            _fakeRecommended = allProjects
                .Where((_, i) => i % 3 == 1).Take(20).ToList();

            _fakeCategoryMap = allProjects
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            menuPanel?.BuildCategories(ProjectCacheRepository.Instance.AllCategories);
            SetTitle("Latest Releases", "latest");
            projectList.Show(_fakeLatest);
        }

        // ── Menu ──────────────────────────────────────────────────────

        private void OnMenuSelectionChanged(HomeMenuSelection selection)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (DevFlags.UseFakeData) { ShowFakeSelection(selection); return; }

            LoadSelectionAsync(selection, _cts.Token).Forget();
        }

        private void ShowFakeSelection(HomeMenuSelection selection)
        {
            projectList.Clear();

            switch (selection.SelectionKind)
            {
                case HomeMenuSelection.Kind.LatestReleases:
                    SetTitle("Latest Releases", "latest");
                    projectList.Show(_fakeLatest);
                    break;

                case HomeMenuSelection.Kind.Recommended:
                    SetTitle("Recommended", "recommended");
                    projectList.Show(_fakeRecommended);
                    break;

                case HomeMenuSelection.Kind.Category:
                    SetTitle(selection.CategoryName, "category");
                    var list = _fakeCategoryMap != null &&
                               _fakeCategoryMap.TryGetValue(selection.CategoryId, out var projects)
                        ? projects
                        : new List<SubscribedProject>();
                    projectList.Show(list);
                    break;
            }
        }

        // ── Load ──────────────────────────────────────────────────────

        private async UniTaskVoid LoadSelectionAsync(HomeMenuSelection selection, CancellationToken ct)
        {
            SetLoading(true);
            projectList.Clear();

            try
            {
                switch (selection.SelectionKind)
                {
                    case HomeMenuSelection.Kind.LatestReleases:
                        SetTitle("Latest Releases", "latest");
                        var latest = await ServiceLocator.Instance.ProjectService
                            .GetLatestAsync(limit: 20, ct: ct);
                        if (ct.IsCancellationRequested) return;
                        projectList.Show(latest);
                        break;

                    case HomeMenuSelection.Kind.Recommended:
                        SetTitle("Recommended", "recommended");
                        var recommended = await ServiceLocator.Instance.ProjectService
                            .GetRecommendedAsync(ct);
                        if (ct.IsCancellationRequested) return;
                        projectList.Show(recommended.Cast<SubscribedProject>().ToList());
                        break;

                    case HomeMenuSelection.Kind.Category:
                        SetTitle(selection.CategoryName, "category");
                        var projects = ProjectCacheRepository.Instance.AllProjects
                            .FindAll(p => p.CategoryId == selection.CategoryId);
                        projectList.Show(projects);
                        break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[HomePage] Load failed: {e.Message}"); }
            finally
            {
                if (!ct.IsCancellationRequested)
                    SetLoading(false);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void SetTitle(string title, string source)
        {
            if (rightPanelTitle != null)
                rightPanelTitle.text = title;

            projectList.OnProjectClicked = project =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam { ProjectId = project.Id, Source = source });
        }

        private void SetLoading(bool isLoading)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(isLoading);
        }
    }
}
