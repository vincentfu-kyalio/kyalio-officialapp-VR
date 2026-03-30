using System.Collections.Generic;
using Kyalio.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Grouped section block for the Series page (series_content.prefab).
    /// Supports two modes driven by GET /api/roles/content:
    ///   - projects mode: Bind(RoleContentItem)   — shows ProjectCards
    ///   - episodes mode: BindEpisodes(RoleContentItem) — shows EpisodeCards
    /// Inspector: titleText, seriesContent, titleButton, episodePrefab, episodeCardPrefab
    /// </summary>
    public class SeriesSection : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform seriesContent;
        [SerializeField] private Button titleButton;
        [SerializeField] private ProjectCard episodePrefab;       // projects mode
        [SerializeField] private EpisodeCard episodeCardPrefab;   // episodes mode

        private string _roleId;

        private readonly List<ProjectCard> _projectPool = new();
        private readonly List<ProjectCard> _projectActive = new();

        private readonly List<EpisodeCard> _episodePool = new();
        private readonly List<EpisodeCard> _episodeActive = new();

        public System.Action<string> OnTitleClicked;
        public System.Action<SubscribedProject> OnProjectClicked;
        public System.Action<RoleContentEpisode> OnEpisodeClicked;

        private void Awake()
        {
            titleButton?.onClick.AddListener(() => OnTitleClicked?.Invoke(_roleId));
        }

        /// <summary>Projects mode — shows a ProjectCard per project.</summary>
        public void Bind(RoleContentItem role)
        {
            _roleId = role.Id;
            titleText.text = role.Name;
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
            _roleId = role.Id;
            titleText.text = role.Name;
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
                card = Instantiate(episodePrefab, seriesContent);
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
