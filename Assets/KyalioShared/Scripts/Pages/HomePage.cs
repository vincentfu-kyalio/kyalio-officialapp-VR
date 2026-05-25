using System.Collections.Generic;
using System.Linq;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.State.V2;
using TMPro;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Home page: two-column layout.
    /// Left  — HomeMenuPanel sidebar: Latest Releases, Recommended, Specialties.
    /// Right — ProjectCardList showing the selected section's projects.
    ///
    /// All data is read from the local V2 cache populated by the login sync triad
    /// (AppState.LastHome + ProjectCacheRepository). No per-page network calls.
    /// Inspector: menuPanel, rightPanelTitle, projectList, loadingIndicator (optional)
    /// </summary>
    public class HomePage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        [SerializeField] private HomeMenuPanel menuPanel;
        [SerializeField] private TextMeshProUGUI rightPanelTitle;
        [SerializeField] private ProjectCardList projectList;
        [SerializeField] private GameObject loadingIndicator;

        private void Awake()
        {
            if (menuPanel != null)
                menuPanel.OnSelectionChanged += OnMenuSelectionChanged;
        }

        public void OnEnter(object param)
        {
            SetLoading(false);

            if (menuPanel != null)
                menuPanel.BuildCategories(ProjectCacheRepository.Instance.Specialties);

            // Always start on Latest Releases when entering the page
            ShowSelection(new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases });
        }

        public void OnExit()
        {
            projectList.Clear();
        }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData() => OnEnter(null);

        // ── Menu ──────────────────────────────────────────────────────

        private void OnMenuSelectionChanged(HomeMenuSelection selection) => ShowSelection(selection);

        private void ShowSelection(HomeMenuSelection selection)
        {
            projectList.Clear();

            var repo = ProjectCacheRepository.Instance;
            var home = AppState.Instance.LastHome;

            switch (selection.SelectionKind)
            {
                case HomeMenuSelection.Kind.LatestReleases:
                    SetTitle("Latest Releases", "latest");
                    projectList.Show(repo.Hydrate(home?.Latest));
                    break;

                case HomeMenuSelection.Kind.Recommended:
                    SetTitle("Recommended", "recommended");
                    projectList.Show(repo.Hydrate(home?.Recommended));
                    break;

                case HomeMenuSelection.Kind.Category:
                    SetTitle(selection.CategoryName, "specialty");
                    var projects = repo.All
                        .Where(p => p.SpecialtyId == selection.CategoryId)
                        .ToList();
                    projectList.Show(projects);
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void SetTitle(string title, string source)
        {
            if (rightPanelTitle != null)
                rightPanelTitle.text = title;

            projectList.OnProjectClicked = project =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam { ProjectId = project.ProjectId, Source = source });
        }

        private void SetLoading(bool isLoading)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(isLoading);
        }
    }
}
