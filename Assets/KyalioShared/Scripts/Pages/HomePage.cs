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
        private Dictionary<string, List<SubscribedProject>> _fakeProgramMap;

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
            var fakeCategories = new List<Category>
            {
                new Category { Id = "cat001", Name = "Cardiology" },
                new Category { Id = "cat002", Name = "Surgery" },
                new Category { Id = "cat003", Name = "General Practice" },
                new Category { Id = "cat004", Name = "Neurology" },
                new Category { Id = "cat005", Name = "Emergency Medicine" },
                new Category { Id = "cat006", Name = "Pediatrics" },
                new Category { Id = "cat007", Name = "Oncology" },
                new Category { Id = "cat008", Name = "Orthopedics" },
                new Category { Id = "cat009", Name = "Radiology" },
                new Category { Id = "cat010", Name = "Dermatology" },
                new Category { Id = "cat011", Name = "ENT" },
                new Category { Id = "cat012", Name = "Ophthalmology" },
                new Category { Id = "cat013", Name = "Psychiatry" },
                new Category { Id = "cat014", Name = "Anesthesiology" },
                new Category { Id = "cat015", Name = "Rehabilitation" },
            };

            var fakePrograms = new List<Category>
            {
                new Category { Id = "prog001", Name = "KyalioMed Basic" },
                new Category { Id = "prog002", Name = "Advanced Surgical Series" },
                new Category { Id = "prog003", Name = "Clinical Skills" },
                new Category { Id = "prog004", Name = "Emergency Response Track" },
                new Category { Id = "prog005", Name = "Residency Prep" },
                new Category { Id = "prog006", Name = "Diagnostic Mastery" },
                new Category { Id = "prog007", Name = "Procedure Lab" },
                new Category { Id = "prog008", Name = "Patient Safety Essentials" },
                new Category { Id = "prog009", Name = "Specialist Deep Dive" },
                new Category { Id = "prog010", Name = "XR Anatomy Atlas" },
                new Category { Id = "prog011", Name = "Clinical Communication" },
                new Category { Id = "prog012", Name = "Case Challenge Series" },
                new Category { Id = "prog013", Name = "Evidence in Practice" },
                new Category { Id = "prog014", Name = "Interdisciplinary Bootcamp" },
                new Category { Id = "prog015", Name = "Board Review Sprint" },
            };

            var drNames = new[]
            {
                "Chen Wei", "Sarah Kim", "Marcus Tan", "Emily Lau", "James Roth",
                "Alicia Wong", "David Huang", "Priya Nair", "Noah Lin", "Hannah Su"
            };

            var projectTopics = new[]
            {
                "Anatomy Essentials", "Clinical Assessment", "Diagnostic Reasoning", "Procedure Fundamentals",
                "Patient Communication", "Acute Care Basics", "Interpretation Workshop", "Simulation Challenge",
                "Emergency Protocol", "Advanced Concepts", "Case Review", "Hands-on Techniques"
            };

            var allProjects = new List<SubscribedProject>();
            for (var i = 1; i <= 75; i++)
            {
                var category = fakeCategories[(i - 1) % fakeCategories.Count];
                var program = fakePrograms[((i - 1) * 2) % fakePrograms.Count];
                var doctor = drNames[(i - 1) % drNames.Length];
                var topic = projectTopics[(i - 1) % projectTopics.Length];

                allProjects.Add(new SubscribedProject
                {
                    Id = $"p{i:000}",
                    Name = $"{category.Name} {topic} {((i - 1) / fakeCategories.Count) + 1}",
                    DrName = doctor,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    ProgramId = program.Id,
                    ProgramName = program.Name,
                    PlaylistDurationSeconds = 900 + ((i - 1) % 8) * 300,
                    PlaylistCount = 1 + ((i - 1) % 6)
                });
            }

            _fakeLatest = allProjects
                .Where((_, index) => index % 3 == 0)
                .Take(20)
                .ToList();

            _fakeRecommended = allProjects
                .Where((_, index) => index % 3 == 1)
                .Take(20)
                .ToList();

            _fakeCategoryMap = allProjects
                .GroupBy(project => project.CategoryId)
                .ToDictionary(group => group.Key, group => group.ToList());

            _fakeProgramMap = allProjects
                .GroupBy(project => project.ProgramId)
                .ToDictionary(group => group.Key, group => group.ToList());

            if (_fakeProgramMap.Count < fakePrograms.Count)
                Debug.LogWarning("[HomePage] Some fake programs have no projects assigned.");

            // Populate the shared cache so other pages (e.g. ProjectInfoPage) can look up
            // fake projects by ID without needing their own copy of the data.
            ProjectCacheRepository.Instance.Build(new List<SubscriptionItem>
            {
                new SubscriptionItem { Projects = allProjects, Categories = fakeCategories }
            });

            menuPanel?.BuildCategories(fakeCategories);
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
