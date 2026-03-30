using System.Collections.Generic;
using Kyalio.Models;
using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Project card list that uses an object pool.
    /// Attach to the container that holds the ScrollRect.
    /// Inspector: cardPrefab, container
    /// </summary>
    public class ProjectCardList : MonoBehaviour
    {
        [SerializeField] private ProjectCard cardPrefab;
        [SerializeField] private Transform container;

        private readonly List<ProjectCard> _pool = new();
        private readonly List<ProjectCard> _active = new();

        // Click callback, set by the page script
        public System.Action<SubscribedProject> OnProjectClicked;

        /// <summary>
        /// Displays the list of projects.
        /// </summary>
        public void Show(IReadOnlyList<SubscribedProject> projects)
        {
            ReturnAll();

            foreach (var project in projects)
            {
                var card = GetCard();
                card.OnClicked = p => OnProjectClicked?.Invoke(p);
                card.Bind(project);
            }
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
            _active.Add(card);
            return card;
        }

        private void ReturnAll()
        {
            foreach (var card in _active)
            {
                card.gameObject.SetActive(false);
                _pool.Add(card);
            }
            _active.Clear();
        }
    }
}
