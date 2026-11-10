using System.Collections.Generic;
using Kyalio.Models.V2;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Project card list that uses an object pool.
    /// Attach to the container that holds the ScrollRect.
    /// Inspector: cardPrefab, container, scrollRect
    /// </summary>
    public class ProjectCardList : MonoBehaviour
    {
        [SerializeField] private ProjectCard cardPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private ScrollRect scrollRect;

        private CardPool<ProjectCard> _pool;
        private CardPool<ProjectCard> Pool => _pool ??= new(cardPrefab, container, c => c.CancelLoads());

        // Click callback, set by the page script
        public System.Action<Project> OnProjectClicked;

        /// <summary>
        /// Displays the list of projects and scrolls to the top.
        /// Pass keepScroll when re-showing the same projects (e.g. returning to a page):
        /// Clear() leaves the content position untouched, so the user lands where they left.
        /// </summary>
        public void Show(IReadOnlyList<Project> projects, bool keepScroll = false)
        {
            int count = projects?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var card = Pool.GetOrReuse(i);
                card.OnClicked = p => OnProjectClicked?.Invoke(p);
                card.Bind(projects[i]);
            }
            Pool.TrimTo(count);

            RefreshLayout(keepScroll);
        }

        /// <summary>
        /// Clears all cards and returns them to the object pool.
        /// </summary>
        public void Clear() => Pool.ReturnAll();

        /// <summary>
        /// Forces the ScrollRect content to rebuild after its cards change and,
        /// unless keepScroll, resets the scroll position to the top. The card container is nested
        /// inside the ScrollRect content, so rebuilding the outer content here is
        /// what propagates the new size up to the ScrollRect — rebuilding only the
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
