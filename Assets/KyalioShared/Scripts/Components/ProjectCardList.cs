using System.Collections.Generic;
using Kyalio.Models.V2;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Virtualised project card grid (see <see cref="VirtualGrid{T}"/>): only the rows in
    /// view have a card, so showing ~165 projects costs about a dozen cards.
    /// Attach to the container that holds the ScrollRect.
    /// Inspector: cardPrefab, container (the GridLayoutGroup), scrollRect
    /// </summary>
    public class ProjectCardList : MonoBehaviour
    {
        [SerializeField] private ProjectCard cardPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("Extra rows kept bound above and below the viewport.")]
        [SerializeField, Min(0)] private int bufferRows = 1;

        // Click callback, set by the page script
        public System.Action<Project> OnProjectClicked;

        private readonly List<Project> _projects = new();
        private VirtualGrid<ProjectCard> _grid;

        // Created lazily: the page may bind before this component's Awake has run.
        private VirtualGrid<ProjectCard> Grid => _grid ??= new VirtualGrid<ProjectCard>(
            (RectTransform)container, cardPrefab, scrollRect, bufferRows,
            onCreate: card => card.OnClicked = p => OnProjectClicked?.Invoke(p),
            bind: (card, i) => card.Bind(_projects[i]),
            onHide: card => card.CancelLoads());

        /// <summary>
        /// Displays the list of projects and scrolls to the top.
        /// Pass keepScroll when re-showing the same projects (e.g. returning to a page):
        /// the content position is left untouched, so the user lands where they left.
        /// </summary>
        public void Show(IReadOnlyList<Project> projects, bool keepScroll = false)
        {
            _projects.Clear();
            if (projects != null) _projects.AddRange(projects);
            Grid.SetCount(_projects.Count, keepScroll);
        }

        /// <summary>Removes every card from view.</summary>
        public void Clear() => Show(null, keepScroll: true);

        private void OnDestroy() => _grid?.Dispose();
    }
}
