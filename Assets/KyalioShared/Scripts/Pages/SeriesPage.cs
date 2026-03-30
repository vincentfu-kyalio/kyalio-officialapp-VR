using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.State;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Series page: fetches GET /api/roles/content and renders one SeriesSection per role.
    /// Supports two backend-controlled modes:
    ///   - "projects": each section shows ProjectCards → tapping navigates to ProjectInfoPage
    ///   - "episodes": each section shows EpisodeCards → tapping plays the video directly
    /// Inspector: scrollContent, seriesSectionPrefab
    /// </summary>
    public class SeriesPage : MonoBehaviour, IPageHandler
    {
        [SerializeField] private Transform scrollContent;
        [SerializeField] private SeriesSection seriesSectionPrefab;

        private const float CacheTtlSeconds = 60f;

        private CancellationTokenSource _cts;
        private readonly List<SeriesSection> _sections = new();
        private float _lastFetchedAt = float.MinValue;

        public void OnEnter(object param)
        {
            if (_sections.Count > 0 &&
                Time.realtimeSinceStartup - _lastFetchedAt < CacheTtlSeconds)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            LoadAsync(_cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            LoadingOverlay.Instance.Show();
            ClearSections();

            try
            {
                var response = await ServiceLocator.Instance.ProjectService
                    .GetRoleContentAsync(ct);

                if (ct.IsCancellationRequested) return;

                _lastFetchedAt = Time.realtimeSinceStartup;

                foreach (var role in response.Items)
                {
                    var section = Instantiate(seriesSectionPrefab, scrollContent);

                    if (response.Mode == "episodes")
                    {
                        if (role.Episodes == null || role.Episodes.Count == 0) continue;

                        section.OnEpisodeClicked = ep =>
                        {
                            PlaybackState.Instance.ClearPlaylist();
                            UIManager.Instance.GoTo(PageType.PlayVideo,
                                new ValueTuple<string, PlaylistItem>(ep.ProjectId, ep));
                        };
                        section.BindEpisodes(role);
                    }
                    else
                    {
                        if (role.Projects == null || role.Projects.Count == 0) continue;

                        section.OnProjectClicked = p =>
                            UIManager.Instance.GoTo(PageType.ProjectInfo,
                                new ProjectNavParam { ProjectId = p.Id, Source = "roles_content" });
                        section.Bind(role);
                    }

                    _sections.Add(section);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"[SeriesPage] LoadAsync failed: {e.Message}");
            }
            finally
            {
                LoadingOverlay.Instance.Hide();
            }
        }

        private void ClearSections()
        {
            foreach (var s in _sections)
                Destroy(s.gameObject);
            _sections.Clear();
        }
    }
}
