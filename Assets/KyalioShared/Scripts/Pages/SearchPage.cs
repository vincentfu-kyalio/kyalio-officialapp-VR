using System.Collections.Generic;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.Repositories;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Search page: two-column filter layout.
    ///
    /// Left column
    ///   Specialty toggle — opens FilterDropdownPanel with Category checkboxes + Done button
    ///   Program toggle   — opens FilterDropdownPanel with Program checkboxes + Done button
    ///   Only one panel is open at a time. Switching toggles saves the current panel's
    ///   selections and applies the filter before opening the new panel.
    ///   Clicking the active toggle off (or pressing Done) also saves and applies.
    ///
    /// Right column  (root ScrollView + VerticalLayoutGroup)
    ///   SpecialtyChipRow — horizontal ScrollView, hidden when no specialty selected
    ///   ProgramChipRow   — horizontal ScrollView, hidden when no program selected
    ///   ProjectCardList  — vertical ScrollView, shows filtered results
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
        private List<Category> _allCategories = new();
        private List<Category> _allPrograms   = new();

        private void Awake()
        {
            specialtyToggle.onValueChanged.AddListener(OnSpecialtyToggleChanged);
            programToggle.onValueChanged.AddListener(OnProgramToggleChanged);

            specialtyPanel.OnDone += OnSpecialtyDone;
            programPanel.OnDone   += OnProgramDone;

            specialtyChipRow.OnClearAll    += () => { _filter.CategoryIds.Clear(); ApplyFilter(); };
            specialtyChipRow.OnChipClicked += id => { _filter.CategoryIds.Remove(id); ApplyFilter(); };

            programChipRow.OnClearAll    += () => { _filter.ProgramIds.Clear(); ApplyFilter(); };
            programChipRow.OnChipClicked += id => { _filter.ProgramIds.Remove(id); ApplyFilter(); };

            projectList.OnProjectClicked = p =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam { ProjectId = p.Id, Source = "search" });
        }

        public void OnEnter(object param)
        {
            // ProjectCacheRepository is populated from the API (real mode) or
            // FakeDataSeeder.Seed() called by DevBootstrapper (fake mode).
            // No branch needed — both paths land in the same repository.
            _allCategories = ProjectCacheRepository.Instance.AllCategories;
            _allPrograms   = ProjectCacheRepository.Instance.AllPrograms;
            ApplyFilter();
        }

        public void OnExit()
        {
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
                specialtyPanel.Build(_allCategories, _filter.CategoryIds);
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
            _filter.CategoryIds = specialtyPanel.GetSelectedIds();
            ApplyFilter();
        }

        private void SaveAndApplyProgramPanel()
        {
            _filter.ProgramIds = programPanel.GetSelectedIds();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var projects = ProjectCacheRepository.Instance.Filter(_filter);
            projectList.Show(projects);
            specialtyChipRow.Bind(_allCategories, _filter.CategoryIds);
            programChipRow.Bind(_allPrograms, _filter.ProgramIds);
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
