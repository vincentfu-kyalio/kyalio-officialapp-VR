using System.Collections.Generic;
using Kyalio.Models;
using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Content area for the Series page right column.
    /// Supports two modes driven by GET /api/roles/content:
    ///   - projects mode: Bind(RoleContentItem)        — shows ProjectCards
    ///   - episodes mode: BindEpisodes(RoleContentItem) — shows EpisodeCards
    /// Title and navigation are owned by SeriesPage; this component only manages card pooling.
    /// Inspector: seriesContent, projectCardPrefab, episodeCardPrefab
    /// </summary>
    public class SeriesSection : MonoBehaviour
    {
        [SerializeField] private Transform seriesContent;
        [SerializeField] private ProjectCard projectCardPrefab;
        [SerializeField] private EpisodeCard episodeCardPrefab;

        private readonly List<ProjectCard> _projectPool  = new();
        private readonly List<ProjectCard> _projectActive = new();

        private readonly List<EpisodeCard> _episodePool  = new();
        private readonly List<EpisodeCard> _episodeActive = new();

        public System.Action<SubscribedProject>  OnProjectClicked;
        public System.Action<RoleContentEpisode> OnEpisodeClicked;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Projects mode — shows a ProjectCard per project.</summary>
        public void Bind(RoleContentItem role)
        {
            ReturnAllEpisodes();
            ReturnAllProjects();

            if (role.Projects == null) return;
            foreach (var p in role.Projects)
            {
                var card = GetProjectCard();
                card.OnClicked = proj => OnProjectClicked?.Invoke(proj);
                card.Bind(p);
            }
        }

        /// <summary>Episodes mode — shows an EpisodeCard per episode.</summary>
        public void BindEpisodes(RoleContentItem role)
        {
            ReturnAllProjects();
            ReturnAllEpisodes();

            if (role.Episodes == null) return;
            foreach (var ep in role.Episodes)
            {
                var card = GetEpisodeCard();
                card.OnClicked = episode => OnEpisodeClicked?.Invoke(episode);
                card.Bind(ep);
            }
        }

        public void Clear()
        {
            ReturnAllProjects();
            ReturnAllEpisodes();
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
            _projectActive.Add(card);
            return card;
        }

        private void ReturnAllProjects()
        {
            foreach (var c in _projectActive)
            {
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
