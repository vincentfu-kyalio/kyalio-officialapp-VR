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

namespace Kyalio.Pages
{
    /// <summary>
    /// Search page: two-column filter layout.
    ///
    /// Left column — Specialty / Program filter toggles, each opening a FilterDropdownPanel.
    /// Right column — selected-filter chip rows + ProjectCardList of results.
    ///
    /// Filter sources (repo.Specialties / repo.Programs) and result projects are scoped to
    /// the member's granted projects. With filters applied the authoritative ordered list
    /// comes from GET /api/projects/search (also yielding searchEventId for analytics);
    /// with no filters we show the whole granted cache. Results are hydrated from the cache.
    ///
    /// Inspector: specialtyToggle, programToggle, specialtyPanel, programPanel,
    ///            specialtyChipRow, programChipRow, projectList
    /// Note: specialtyPanel and programPanel must start as inactive in the Inspector.
    /// </summary>
    public class SearchPage : MonoBehaviour, IPageHandler
    {
        [Header("Left Column")]
        [SerializeField] private Toggle specialtyToggle;
        [SerializeField] private Toggle programToggle;
        [SerializeField] private FilterDropdownPanel specialtyPanel;
        [SerializeField] private FilterDropdownPanel programPanel;

        [Header("Right Column")]
        [SerializeField] private FilterChipRow specialtyChipRow;
        [SerializeField] private FilterChipRow programChipRow;
        [SerializeField] private ProjectCardList projectList;

        private readonly FilterOptions _filter = new();
        private List<IdNameRef> _allSpecialties = new();
        private List<IdNameRef> _allPrograms    = new();
        private string _searchEventId;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            specialtyToggle.onValueChanged.AddListener(OnSpecialtyToggleChanged);
            programToggle.onValueChanged.AddListener(OnProgramToggleChanged);

            specialtyPanel.OnDone += OnSpecialtyDone;
            programPanel.OnDone   += OnProgramDone;

            specialtyChipRow.OnClearAll    += () => { _filter.SpecialtyIds.Clear(); ApplyFilter(); };
            specialtyChipRow.OnChipClicked += id => { _filter.SpecialtyIds.Remove(id); ApplyFilter(); };

            programChipRow.OnClearAll    += () => { _filter.ProgramIds.Clear(); ApplyFilter(); };
            programChipRow.OnChipClicked += id => { _filter.ProgramIds.Remove(id); ApplyFilter(); };

            projectList.OnProjectClicked = p =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam
                    {
                        ProjectId     = p.ProjectId,
                        Source        = ProjectPageSource.Search,
                        SearchEventId = _searchEventId,
                    });
        }

        public void OnEnter(object param)
        {
            var repo = ProjectCacheRepository.Instance;
            _allSpecialties = repo.Specialties;
            _allPrograms    = repo.Programs
                .Select(p => new IdNameRef { Id = p.Id, Name = p.Name })
                .ToList();
            ApplyFilter();
        }

        public void OnExit()
        {
            _cts?.Cancel();
            CloseAllPanels();
        }

        // ── Left column — Toggle logic ────────────────────────────────

        private void OnSpecialtyToggleChanged(bool isOn)
        {
            if (isOn)
            {
                if (programToggle.isOn)
                {
                    SaveAndApplyProgramPanel();
                    programToggle.SetIsOnWithoutNotify(false);
                    programPanel.gameObject.SetActive(false);
                }
                specialtyPanel.Build(_allSpecialties, _filter.SpecialtyIds);
                specialtyPanel.gameObject.SetActive(true);
            }
            else
            {
                SaveAndApplySpecialtyPanel();
                specialtyPanel.gameObject.SetActive(false);
            }
        }

        private void OnProgramToggleChanged(bool isOn)
        {
            if (isOn)
            {
                if (specialtyToggle.isOn)
                {
                    SaveAndApplySpecialtyPanel();
                    specialtyToggle.SetIsOnWithoutNotify(false);
                    specialtyPanel.gameObject.SetActive(false);
                }
                programPanel.Build(_allPrograms, _filter.ProgramIds);
                programPanel.gameObject.SetActive(true);
            }
            else
            {
                SaveAndApplyProgramPanel();
                programPanel.gameObject.SetActive(false);
            }
        }

        private void OnSpecialtyDone()
        {
            SaveAndApplySpecialtyPanel();
            specialtyToggle.SetIsOnWithoutNotify(false);
            specialtyPanel.gameObject.SetActive(false);
        }

        private void OnProgramDone()
        {
            SaveAndApplyProgramPanel();
            programToggle.SetIsOnWithoutNotify(false);
            programPanel.gameObject.SetActive(false);
        }

        // ── Filter ────────────────────────────────────────────────────

        private void SaveAndApplySpecialtyPanel()
        {
            _filter.SpecialtyIds = specialtyPanel.GetSelectedIds();
            ApplyFilter();
        }

        private void SaveAndApplyProgramPanel()
        {
            _filter.ProgramIds = programPanel.GetSelectedIds();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            specialtyChipRow.Bind(_allSpecialties, _filter.SpecialtyIds);
            programChipRow.Bind(_allPrograms, _filter.ProgramIds);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            ApplyFilterAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid ApplyFilterAsync(CancellationToken ct)
        {
            var repo = ProjectCacheRepository.Instance;

            // No filters: show the whole granted cache without a round-trip.
            if (_filter.IsEmpty)
            {
                _searchEventId = null;
                projectList.Show(repo.All.ToList());
                return;
            }

            // Dev mode has no server — filter the seeded cache locally.
            if (DevFlags.UseFakeData)
            {
                _searchEventId = null;
                projectList.Show(repo.Filter(_filter));
                return;
            }

            try
            {
                var response = await ServiceLocator.Instance.V2.Content.SearchAsync(
                    specialtyIds: _filter.SpecialtyIds,
                    programIds:   _filter.ProgramIds,
                    ct:           ct);
                if (ct.IsCancellationRequested) return;

                _searchEventId = response?.SearchEventId;
                projectList.Show(repo.Hydrate(response?.Items));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // Fall back to client-side filtering so search still works offline.
                Debug.LogWarning($"[SearchPage] Search request failed, using local filter: {e.Message}");
                _searchEventId = null;
                projectList.Show(repo.Filter(_filter));
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void CloseAllPanels()
        {
            if (specialtyToggle.isOn)
            {
                specialtyToggle.SetIsOnWithoutNotify(false);
                specialtyPanel.gameObject.SetActive(false);
            }
            if (programToggle.isOn)
            {
                programToggle.SetIsOnWithoutNotify(false);
                programPanel.gameObject.SetActive(false);
            }
        }
    }
}
