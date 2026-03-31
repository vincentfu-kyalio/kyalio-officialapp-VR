using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Dev;
using Kyalio.Models;
using Kyalio.State;
using TMPro;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Series page: two-column layout.
    /// Left  — SeriesRolePanel sidebar: one button per role.
    /// Right — SeriesSection showing projects or episodes for the selected role.
    /// Mode ("projects" / "episodes") is determined by the API response and
    /// applied consistently to every role selection.
    /// Inspector: rolePanel, rightPanelTitle, rightSection
    /// </summary>
    public class SeriesPage : MonoBehaviour, IPageHandler, IDevFakeData
    {
        [SerializeField] private SeriesRolePanel rolePanel;
        [SerializeField] private TextMeshProUGUI rightPanelTitle;
        [SerializeField] private SeriesSection rightSection;

        private const float CacheTtlSeconds = 60f;

        private CancellationTokenSource _cts;
        private List<RoleContentItem> _roles;
        private string _responseMode = "projects";
        private float _lastFetchedAt = float.MinValue;

        private void Awake()
        {
            if (rolePanel != null)
                rolePanel.OnRoleSelected += OnRoleSelected;
        }

        public void OnEnter(object param)
        {
            if (DevFlags.UseFakeData) { LoadFakeData(); return; }

            // Roles already cached — re-select the first to refresh the right panel
            if (_roles != null && Time.realtimeSinceStartup - _lastFetchedAt < CacheTtlSeconds)
            {
                rolePanel.SelectFirst();
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            LoadAsync(_cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
        }

        [ContextMenu("Load Fake Data")]
        public void LoadFakeData()
        {
            var roles = new List<RoleContentItem>
            {
                new RoleContentItem
                {
                    Id = "fake-role-001", Name = "Cardiology",
                    Projects = new List<SubscribedProject>
                    {
                        new SubscribedProject { Id = "p001", Name = "Heart Anatomy VR",           DrName = "Chen Wei",   CategoryName = "Cardiology", PlaylistDurationSeconds = 2400, PlaylistCount = 3 },
                        new SubscribedProject { Id = "p002", Name = "ECG Interpretation",         DrName = "Sarah Kim",  CategoryName = "Cardiology", PlaylistDurationSeconds = 1500, PlaylistCount = 2 },
                        new SubscribedProject { Id = "p003", Name = "Cardiac Surgery Simulation", DrName = "Marcus Tan", CategoryName = "Cardiology", PlaylistDurationSeconds = 3600, PlaylistCount = 5 },
                    }
                },
                new RoleContentItem
                {
                    Id = "fake-role-002", Name = "Neurology",
                    Projects = new List<SubscribedProject>
                    {
                        new SubscribedProject { Id = "p004", Name = "Brain MRI Interpretation",   DrName = "James Roth", CategoryName = "Neurology",  PlaylistDurationSeconds = 2700, PlaylistCount = 4 },
                        new SubscribedProject { Id = "p005", Name = "Stroke Response Training",   DrName = "James Roth", CategoryName = "Neurology",  PlaylistDurationSeconds = 1800, PlaylistCount = 2 },
                    }
                },
                new RoleContentItem
                {
                    Id = "fake-role-003", Name = "Surgery",
                    Projects = new List<SubscribedProject>
                    {
                        new SubscribedProject { Id = "p006", Name = "Laparoscopic Techniques",    DrName = "Marcus Tan", CategoryName = "Surgery",    PlaylistDurationSeconds = 3000, PlaylistCount = 4 },
                        new SubscribedProject { Id = "p007", Name = "Wound Management",           DrName = "Emily Lau",  CategoryName = "Surgery",    PlaylistDurationSeconds = 1200, PlaylistCount = 2 },
                    }
                },
                new RoleContentItem
                {
                    Id = "fake-role-004", Name = "General Practice",
                    Projects = new List<SubscribedProject>
                    {
                        new SubscribedProject { Id = "p008", Name = "Patient Communication in VR", DrName = "Emily Lau",  CategoryName = "General Practice", PlaylistDurationSeconds = 1200, PlaylistCount = 1 },
                        new SubscribedProject { Id = "p009", Name = "Preventive Medicine Basics",  DrName = "Alicia Wong",CategoryName = "General Practice", PlaylistDurationSeconds = 1800, PlaylistCount = 3 },
                    }
                },
            };

            _responseMode = "projects";
            SetupRoles(roles);
            rolePanel.SelectFirst();
        }

        // ── Load ──────────────────────────────────────────────────────

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            LoadingOverlay.Instance.Show();
            try
            {
                var response = await ServiceLocator.Instance.ProjectService
                    .GetRoleContentAsync(ct);

                if (ct.IsCancellationRequested) return;

                _lastFetchedAt = Time.realtimeSinceStartup;
                _responseMode  = response.Mode ?? "projects";
                SetupRoles(response.Items);
                rolePanel.SelectFirst();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[SeriesPage] Load failed: {e.Message}"); }
            finally { LoadingOverlay.Instance.Hide(); }
        }

        private void SetupRoles(List<RoleContentItem> roles)
        {
            _roles = roles;
            rightSection?.Clear();
            rolePanel?.Build(roles);
        }

        // ── Role selection ────────────────────────────────────────────

        private void OnRoleSelected(RoleContentItem role)
        {
            if (rightPanelTitle != null)
                rightPanelTitle.text = role.Name;

            rightSection.Clear();

            if (_responseMode == "episodes")
            {
                rightSection.OnEpisodeClicked = ep =>
                {
                    PlaybackState.Instance.ClearPlaylist();
                    UIManager.Instance.GoTo(PageType.PlayVideo,
                        new ValueTuple<string, PlaylistItem>(ep.ProjectId, ep));
                };
                rightSection.BindEpisodes(role);
            }
            else
            {
                rightSection.OnProjectClicked = p =>
                    UIManager.Instance.GoTo(PageType.ProjectInfo,
                        new ProjectNavParam { ProjectId = p.Id, Source = "roles_content" });
                rightSection.Bind(role);
            }
        }
    }
}
