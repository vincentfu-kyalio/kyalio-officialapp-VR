using System.Collections.Generic;
using Kyalio.Models.V2;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Content area for the Series page right column.
    /// Supports the two home-roles display modes:
    ///   - projects mode: Bind(projects)        — shows ProjectCards
    ///   - episodes mode: BindEpisodes(episodes) — shows EpisodeCards
    /// Title and navigation are owned by SeriesPage; this component only manages card pooling.
    /// Inspector: seriesContent, projectCardPrefab, episodeCardPrefab, scrollRect
    /// </summary>
    public class SeriesSection : MonoBehaviour
    {
        [SerializeField] private Transform seriesContent;
        [SerializeField] private ProjectCard projectCardPrefab;
        [SerializeField] private EpisodeCard episodeCardPrefab;
        [SerializeField] private ScrollRect scrollRect;

        private readonly List<ProjectCard> _projectPool  = new();
        private readonly List<ProjectCard> _projectActive = new();

        private readonly List<EpisodeCard> _episodePool  = new();
        private readonly List<EpisodeCard> _episodeActive = new();

        public System.Action<Project> OnProjectClicked;

        /// <summary>(projectId, video) of the clicked episode.</summary>
        public System.Action<string, PlaylistItem> OnEpisodeClicked;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Projects mode — shows a ProjectCard per project.</summary>
        public void Bind(IReadOnlyList<Project> projects)
        {
            ReturnAllEpisodes();
            ReturnAllProjects();

            if (projects == null) return;
            foreach (var p in projects)
            {
                var card = GetProjectCard();
                card.OnClicked = proj => OnProjectClicked?.Invoke(proj);
                card.Bind(p);
            }

            RefreshLayout();
        }

        /// <summary>Episodes mode — shows an EpisodeCard per (project, video, progress).</summary>
        public void BindEpisodes(IReadOnlyList<(string projectId, PlaylistItem item, int progressMs)> episodes)
        {
            ReturnAllProjects();
            ReturnAllEpisodes();

            if (episodes == null) return;
            foreach (var ep in episodes)
            {
                if (ep.item == null) continue;
                var card = GetEpisodeCard();
                card.OnClicked = (pid, item) => OnEpisodeClicked?.Invoke(pid, item);
                card.Bind(ep.projectId, ep.item, ep.progressMs);
            }

            RefreshLayout();
        }

        public void Clear()
        {
            ReturnAllProjects();
            ReturnAllEpisodes();
        }

        /// <summary>
        /// Forces the ScrollRect content to rebuild after its cards change and
        /// resets the scroll position to the top. seriesContent is nested inside
        /// the ScrollRect content, so rebuilding the outer content is what
        /// propagates the new size up to the ScrollRect — rebuilding only the
        /// inner container leaves the scroll view blank or clipped.
        /// </summary>
        private void RefreshLayout()
        {
            if (scrollRect == null) return;
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // ── Project card pool ─────────────────────────────────────────

        private ProjectCard GetProjectCard()
        {
            ProjectCard card;
            if (_projectPool.Count > 0)
            {
                card = _projectPool[^1];
                _projectPool.RemoveAt(_projectPool.Count - 1);
            }
            else
            {
                card = Instantiate(projectCardPrefab, seriesContent);
            }
            card.gameObject.SetActive(true);
            card.transform.SetAsLastSibling();
            _projectActive.Add(card);
            return card;
        }

        private void ReturnAllProjects()
        {
            foreach (var c in _projectActive)
            {
                c.CancelLoads();
                c.gameObject.SetActive(false);
                _projectPool.Add(c);
            }
            _projectActive.Clear();
        }

        // ── Episode card pool ─────────────────────────────────────────

        private EpisodeCard GetEpisodeCard()
        {
            EpisodeCard card;
            if (_episodePool.Count > 0)
            {
                card = _episodePool[^1];
                _episodePool.RemoveAt(_episodePool.Count - 1);
            }
            else
            {
                card = Instantiate(episodeCardPrefab, seriesContent);
            }
            card.gameObject.SetActive(true);
            card.transform.SetAsLastSibling();
            _episodeActive.Add(card);
            return card;
        }

        private void ReturnAllEpisodes()
        {
            foreach (var c in _episodeActive)
            {
                c.gameObject.SetActive(false);
                _episodePool.Add(c);
            }
            _episodeActive.Clear();
        }
    }
}
