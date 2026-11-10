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
    /// Title and navigation are owned by SeriesPage; this component only manages the cards.
    ///
    /// Both modes are virtualised (see <see cref="VirtualGrid{T}"/>) and share seriesContent
    /// and its GridLayoutGroup; only one has items at a time.
    /// Inspector: seriesContent, projectCardPrefab, episodeCardPrefab, scrollRect
    /// </summary>
    public class SeriesSection : MonoBehaviour
    {
        [SerializeField] private Transform seriesContent;
        [SerializeField] private ProjectCard projectCardPrefab;
        [SerializeField] private EpisodeCard episodeCardPrefab;
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Extra rows kept bound above and below the viewport.")]
        [SerializeField, Min(0)] private int bufferRows = 1;

        public System.Action<Project> OnProjectClicked;

        /// <summary>(projectId, video) of the clicked episode.</summary>
        public System.Action<string, PlaylistItem> OnEpisodeClicked;

        private readonly List<Project> _projects = new();
        private readonly List<(string projectId, PlaylistItem item, int progressMs)> _episodes = new();

        private VirtualGrid<ProjectCard> _projectGrid;
        private VirtualGrid<EpisodeCard> _episodeGrid;

        // Created lazily: the page may bind before this component's Awake has run.
        private VirtualGrid<ProjectCard> ProjectGrid => _projectGrid ??= new VirtualGrid<ProjectCard>(
            (RectTransform)seriesContent, projectCardPrefab, scrollRect, bufferRows,
            onCreate: card => card.OnClicked = proj => OnProjectClicked?.Invoke(proj),
            bind: (card, i) => card.Bind(_projects[i]),
            onHide: card => card.CancelLoads());

        private VirtualGrid<EpisodeCard> EpisodeGrid => _episodeGrid ??= new VirtualGrid<EpisodeCard>(
            (RectTransform)seriesContent, episodeCardPrefab, scrollRect, bufferRows,
            onCreate: card => card.OnClicked = (pid, item) => OnEpisodeClicked?.Invoke(pid, item),
            bind: (card, i) => card.Bind(_episodes[i].projectId, _episodes[i].item, _episodes[i].progressMs),
            onHide: card => card.CancelLoads());

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Projects mode — shows a ProjectCard per project and scrolls to the top.
        /// Pass keepScroll when re-showing the same role (e.g. returning to the page):
        /// the content position is left untouched, so the user lands where they left.
        /// </summary>
        public void Bind(IReadOnlyList<Project> projects, bool keepScroll = false)
        {
            // Empty the other mode first: both grids size the same container.
            _episodes.Clear();
            if (_episodeGrid != null && _episodeGrid.Count > 0) _episodeGrid.SetCount(0, keepScroll: true);

            _projects.Clear();
            if (projects != null) _projects.AddRange(projects);
            ProjectGrid.SetCount(_projects.Count, keepScroll);
        }

        /// <summary>Episodes mode — shows an EpisodeCard per (project, video, progress). keepScroll as in Bind.</summary>
        public void BindEpisodes(IReadOnlyList<(string projectId, PlaylistItem item, int progressMs)> episodes,
            bool keepScroll = false)
        {
            _projects.Clear();
            if (_projectGrid != null && _projectGrid.Count > 0) _projectGrid.SetCount(0, keepScroll: true);

            _episodes.Clear();
            if (episodes != null)
                foreach (var ep in episodes)
                    if (ep.item != null) _episodes.Add(ep);
            EpisodeGrid.SetCount(_episodes.Count, keepScroll);
        }

        public void Clear()
        {
            _projects.Clear();
            _episodes.Clear();
            if (_projectGrid != null) _projectGrid.SetCount(0, keepScroll: true);
            if (_episodeGrid != null) _episodeGrid.SetCount(0, keepScroll: true);
        }

        private void OnDestroy()
        {
            _projectGrid?.Dispose();
            _episodeGrid?.Dispose();
        }
    }
}
