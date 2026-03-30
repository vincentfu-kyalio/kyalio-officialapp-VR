using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.Repositories;
using Kyalio.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Home page: all sections (Latest, Recommended, Categories) are created at once.
    /// Content is bound lazily when a section enters the viewport.
    /// Clicking a menu item scrolls to the corresponding section.
    /// Inspector: menuPanel, mainScrollRect, verticalContent, topicSectionPrefab
    /// </summary>
    public class HomePage : MonoBehaviour, IPageHandler
    {
        [SerializeField] private HomeMenuPanel menuPanel;
        [SerializeField] private ScrollRect mainScrollRect;
        [SerializeField] private Transform verticalContent;
        [SerializeField] private TopicSection topicSectionPrefab;

        [Header("VR Pairing")]
        [SerializeField] private Button authVrButton;
        [SerializeField] private EnterCodePage enterCodePage;

        private CancellationTokenSource _cts;

        private class SectionEntry
        {
            public TopicSection Section;
            public HomeMenuSelection MenuSelection;
            public bool IsLoading;
        }

        private readonly List<SectionEntry> _entries = new();

        private void Awake()
        {
            if (menuPanel != null)
                menuPanel.OnSelectionChanged += OnMenuSelectionChanged;

            if (authVrButton != null)
                authVrButton.onClick.AddListener(() => enterCodePage?.Open());
        }

        public void OnEnter(object param)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (menuPanel != null)
                menuPanel.BuildCategories(ProjectCacheRepository.Instance.AllCategories);

            // Only build sections on first enter; IsBound guards prevent re-fetching on subsequent visits
            if (_entries.Count == 0)
                BuildSections();

            mainScrollRect.onValueChanged.AddListener(OnScrollChanged);
            InitialCheckAsync(_cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
            mainScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            foreach (var entry in _entries)
                entry.Section.CancelAllLoads();
        }

        // ── Menu ──────────────────────────────────────────────────────

        private void OnMenuSelectionChanged(HomeMenuSelection selection)
        {
            var entry = _entries.Find(e => SelectionMatches(e.MenuSelection, selection));
            if (entry != null)
                ScrollToSectionAsync(entry.Section, _cts.Token).Forget();
        }

        // ── Build ─────────────────────────────────────────────────────

        private void BuildSections()
        {
            ClearSections();
            _entries.Clear();

            _entries.Add(new SectionEntry
            {
                Section = CreateSection("Latest Releases", "latest"),
                MenuSelection = new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.LatestReleases }
            });

            _entries.Add(new SectionEntry
            {
                Section = CreateSection("Recommended", "recommended"),
                MenuSelection = new HomeMenuSelection { SelectionKind = HomeMenuSelection.Kind.Recommended }
            });

            foreach (var cat in ProjectCacheRepository.Instance.AllCategories)
            {
                bool hasProjects = ProjectCacheRepository.Instance.AllProjects
                    .Any(p => p.CategoryId == cat.Id);
                if (!hasProjects) continue;

                _entries.Add(new SectionEntry
                {
                    Section = CreateSection(cat.Name, "category"),
                    MenuSelection = new HomeMenuSelection
                    {
                        SelectionKind = HomeMenuSelection.Kind.Category,
                        CategoryId    = cat.Id,
                        CategoryName  = cat.Name
                    }
                });
            }
        }

        // ── Viewport lazy load ────────────────────────────────────────

        private void OnScrollChanged(Vector2 _) => CheckVisibleSections();

        private async UniTaskVoid InitialCheckAsync(CancellationToken ct)
        {
            // Wait one frame for Unity to finish calculating layout positions
            await UniTask.NextFrame(ct);
            if (!ct.IsCancellationRequested)
                CheckVisibleSections();
        }

        private void CheckVisibleSections()
        {
            var viewport = mainScrollRect.viewport;
            foreach (var entry in _entries)
            {
                if (entry.Section.IsBound || entry.IsLoading) continue;
                if (!IsInViewport(entry.Section.GetComponent<RectTransform>(), viewport)) continue;
                LoadEntry(entry, _cts.Token);
            }
        }

        private void LoadEntry(SectionEntry entry, CancellationToken ct)
        {
            entry.IsLoading = true;

            switch (entry.MenuSelection.SelectionKind)
            {
                case HomeMenuSelection.Kind.LatestReleases:
                    LoadLatestAsync(entry, ct).Forget();
                    break;
                case HomeMenuSelection.Kind.Recommended:
                    LoadRecommendedAsync(entry, ct).Forget();
                    break;
                case HomeMenuSelection.Kind.Category:
                    var projects = ProjectCacheRepository.Instance.AllProjects
                        .FindAll(p => p.CategoryId == entry.MenuSelection.CategoryId);
                    entry.Section.Bind(entry.MenuSelection.CategoryName, projects);
                    entry.IsLoading = false;
                    break;
            }
        }

        // ── Data loaders ──────────────────────────────────────────────

        private async UniTaskVoid LoadLatestAsync(SectionEntry entry, CancellationToken ct)
        {
            try
            {
                var latest = await ServiceLocator.Instance.ProjectService
                    .GetLatestAsync(limit: 20, ct: ct);
                if (ct.IsCancellationRequested) return;
                if (latest.Count > 0)
                    entry.Section.Bind("Latest Releases", latest);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[HomePage] LoadLatest failed: {e.Message}"); }
            finally { entry.IsLoading = false; }
        }

        private async UniTaskVoid LoadRecommendedAsync(SectionEntry entry, CancellationToken ct)
        {
            try
            {
                var recommended = await ServiceLocator.Instance.ProjectService
                    .GetRecommendedAsync(ct);
                if (ct.IsCancellationRequested) return;
                if (recommended.Count > 0)
                    entry.Section.Bind("Recommended", recommended.Cast<SubscribedProject>().ToList());
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[HomePage] LoadRecommended failed: {e.Message}"); }
            finally { entry.IsLoading = false; }
        }

        // ── Scroll to section ─────────────────────────────────────────

        private async UniTaskVoid ScrollToSectionAsync(TopicSection section, CancellationToken ct)
        {
            // Trigger load if not yet bound so content is ready after scroll
            var entry = _entries.Find(e => e.Section == section);
            if (entry != null && !entry.Section.IsBound && !entry.IsLoading)
                LoadEntry(entry, ct);

            // Wait a frame for layout to reflect any new content
            await UniTask.NextFrame(ct);
            if (ct.IsCancellationRequested) return;

            var contentRect  = (RectTransform)verticalContent;
            var viewportRect = mainScrollRect.viewport;
            var sectionRect  = section.GetComponent<RectTransform>();

            float scrollable = contentRect.rect.height - viewportRect.rect.height;
            if (scrollable <= 0f) return;

            // anchoredPosition.y is negative going downward; negate to get distance from top
            float distFromTop = -sectionRect.anchoredPosition.y;
            float targetNorm  = Mathf.Clamp01(distFromTop / scrollable);

            // verticalNormalizedPosition: 1 = top, 0 = bottom
            mainScrollRect.verticalNormalizedPosition = 1f - targetNorm;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private TopicSection CreateSection(string title, string source = "direct")
        {
            var section = Instantiate(topicSectionPrefab, verticalContent);
            section.OnProjectClicked = project =>
                UIManager.Instance.GoTo(PageType.ProjectInfo,
                    new ProjectNavParam { ProjectId = project.Id, Source = source });
            section.SetTitle(title);
            return section;
        }

        private void ClearSections()
        {
            foreach (Transform child in verticalContent)
                Destroy(child.gameObject);
        }

        private static bool IsInViewport(RectTransform rect, RectTransform viewport)
        {
            var corners   = new Vector3[4];
            var vpCorners = new Vector3[4];
            rect.GetWorldCorners(corners);
            viewport.GetWorldCorners(vpCorners);

            // corners[0] = bottom-left, corners[1] = top-left
            // Visible when section top > viewport bottom AND section bottom < viewport top
            return corners[1].y > vpCorners[0].y && corners[0].y < vpCorners[1].y;
        }

        private static bool SelectionMatches(HomeMenuSelection a, HomeMenuSelection b)
        {
            if (a.SelectionKind != b.SelectionKind) return false;
            if (a.SelectionKind == HomeMenuSelection.Kind.Category)
                return a.CategoryId == b.CategoryId;
            return true;
        }
    }
}
