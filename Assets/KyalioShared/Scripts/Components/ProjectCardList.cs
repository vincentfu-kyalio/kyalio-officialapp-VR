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

        private readonly List<ProjectCard> _pool = new();
        private readonly List<ProjectCard> _active = new();

        // Click callback, set by the page script
        public System.Action<Project> OnProjectClicked;

        /// <summary>
        /// Displays the list of projects.
        /// </summary>
        public void Show(IReadOnlyList<Project> projects)
        {
            ReturnAll();

            foreach (var project in projects)
            {
                var card = GetCard();
                card.OnClicked = p => OnProjectClicked?.Invoke(p);
                card.Bind(project);
            }

            RefreshLayout();
        }

        /// <summary>
        /// Clears all cards and returns them to the object pool.
        /// </summary>
        public void Clear() => ReturnAll();

        // ── Object Pool ───────────────────────────────────────────────

        private ProjectCard GetCard()
        {
            ProjectCard card;
            if (_pool.Count > 0)
            {
                card = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                card = Instantiate(cardPrefab, container);
            }

            card.gameObject.SetActive(true);
            // Keep hierarchy order aligned with data order so the layout group renders
            // cards consistently. The pool returns cards LIFO, so without this the
            // visible order would flip each time the same list is shown again.
            card.transform.SetAsLastSibling();
            _active.Add(card);
            return card;
        }

        /// <summary>
        /// Forces the ScrollRect content to rebuild after its cards change and
        /// resets the scroll position to the top. The card container is nested
        /// inside the ScrollRect content, so rebuilding the outer content here is
        /// what propagates the new size up to the ScrollRect — rebuilding only the
        /// inner container leaves the scroll view blank or clipped.
        /// </summary>
        private void RefreshLayout()
        {
            if (scrollRect == null) return;
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ReturnAll()
        {
            foreach (var card in _active)
            {
                card.CancelLoads();
                card.gameObject.SetActive(false);
                _pool.Add(card);
            }
            _active.Clear();
        }
    }
}
