using System;
using System.Collections.Generic;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.State;
using TMPro;
using UnityEngine;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Pages
{
    /// <summary>
    /// Series page: two-column layout.
    /// Left  — SeriesRolePanel sidebar: one button per role.
    /// Right — SeriesSection showing projects or episodes for the selected role.
    ///
    /// Source data is the home payload's roles block (AppState.LastHome.Roles), whose
    /// displayMode ("projects" / "episodes") decides which collection each role renders.
    /// All ids are hydrated against the local Project cache.
    /// Inspector: rolePanel, rightPanelTitle, rightSection
    /// </summary>
    public class SeriesPage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        [SerializeField] private SeriesRolePanel rolePanel;
        [SerializeField] private TextMeshProUGUI rightPanelTitle;
        [SerializeField] private SeriesSection rightSection;

        private string _displayMode = HomeRolesDisplayMode.Projects;

        private void Awake()
        {
            if (rolePanel != null)
                rolePanel.OnRoleSelected += OnRoleSelected;
        }

        public void OnEnter(object param)
        {
            var roles = AppState.Instance.LastHome?.Roles;

            _displayMode = roles?.DisplayMode ?? HomeRolesDisplayMode.Projects;

            rightSection?.Clear();
            rolePanel?.Build(roles?.Items ?? new List<HomeRoleItem>());
            rolePanel?.SelectFirst();
        }

        public void OnExit() { }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData() => OnEnter(null);

        // ── Role selection ────────────────────────────────────────────

        private void OnRoleSelected(HomeRoleItem role)
        {
            if (rightPanelTitle != null)
                rightPanelTitle.text = role.Name;

            rightSection.Clear();
            var repo = ProjectCacheRepository.Instance;

            if (_displayMode == HomeRolesDisplayMode.Episodes)
            {
                rightSection.OnEpisodeClicked = (projectId, item) =>
                {
                    PlaybackState.Instance.ClearPlaylist();
                    UIManager.Instance.GoTo(PageType.PlayVideo,
                        new ValueTuple<string, PlaylistItem>(projectId, item),
                        fade: true);
                };

                var episodes = new List<(string, PlaylistItem, int)>();
                foreach (var ep in role.Episodes ?? new List<EpisodeRef>())
                {
                    var item = repo.GetVideo(ep.ProjectId, ep.VideoId);
                    if (item == null) continue;
                    episodes.Add((ep.ProjectId, item, repo.GetProgressMs(ep.VideoId)));
                }
                rightSection.BindEpisodes(episodes);
            }
            else
            {
                rightSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new Kyalio.Models.ProjectNavParam { ProjectId = p.ProjectId, Source = ProjectPageSource.Roles });
                rightSection.Bind(repo.Hydrate(role.ProjectIds));
            }
        }
    }
}
