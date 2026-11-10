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

        private CardPool<ProjectCard> _projectPool;
        private CardPool<EpisodeCard> _episodePool;
        private CardPool<ProjectCard> ProjectPool => _projectPool ??= new(projectCardPrefab, seriesContent, c => c.CancelLoads());
        private CardPool<EpisodeCard> EpisodePool => _episodePool ??= new(episodeCardPrefab, seriesContent, c => c.CancelLoads());

        public System.Action<Project> OnProjectClicked;

        /// <summary>(projectId, video) of the clicked episode.</summary>
        public System.Action<string, PlaylistItem> OnEpisodeClicked;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Projects mode — shows a ProjectCard per project and scrolls to the top.
        /// Pass keepScroll when re-showing the same role (e.g. returning to the page):
        /// the content position is left untouched, so the user lands where they left.
        /// Cards already showing are rebound in place (CardPool.GetOrReuse).
        /// </summary>
        public void Bind(IReadOnlyList<Project> projects, bool keepScroll = false)
        {
            EpisodePool.ReturnAll();

            int count = projects?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var card = ProjectPool.GetOrReuse(i);
                card.OnClicked = proj => OnProjectClicked?.Invoke(proj);
                card.Bind(projects[i]);
            }
            ProjectPool.TrimTo(count);

            RefreshLayout(keepScroll);
        }

        /// <summary>Episodes mode — shows an EpisodeCard per (project, video, progress). keepScroll as in Bind.</summary>
        public void BindEpisodes(IReadOnlyList<(string projectId, PlaylistItem item, int progressMs)> episodes,
            bool keepScroll = false)
        {
            ProjectPool.ReturnAll();

            int count = 0;
            if (episodes != null)
            {
                foreach (var ep in episodes)
                {
                    if (ep.item == null) continue;
                    var card = EpisodePool.GetOrReuse(count++);
                    card.OnClicked = (pid, item) => OnEpisodeClicked?.Invoke(pid, item);
                    card.Bind(ep.projectId, ep.item, ep.progressMs);
                }
            }
            EpisodePool.TrimTo(count);

            RefreshLayout(keepScroll);
        }

        public void Clear()
        {
            ProjectPool.ReturnAll();
            EpisodePool.ReturnAll();
        }

        /// <summary>
        /// Forces the ScrollRect content to rebuild after its cards change and,
        /// unless keepScroll, resets the scroll position to the top. seriesContent is nested inside
        /// the ScrollRect content, so rebuilding the outer content is what
        /// propagates the new size up to the ScrollRect — rebuilding only the
        /// inner container leaves the scroll view blank or clipped.
        /// </summary>
        private void RefreshLayout(bool keepScroll)
        {
            if (scrollRect == null) return;
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            if (!keepScroll)
                scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
