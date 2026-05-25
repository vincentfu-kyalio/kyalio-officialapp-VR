using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Home page horizontal scrolling section (topic_content.prefab).
    /// Contains a title, an optional "See All" button, and a horizontal ScrollRect that holds project_block cards.
    /// Inspector: titleText, seeAllButton (optional), slideContent, cardPrefab
    /// </summary>
    public class TopicSection : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button seeAllButton;   // optional; shown when content is bound
        [SerializeField] private Transform slideContent;
        [SerializeField] private ProjectCard cardPrefab;

        private readonly List<ProjectCard> _pool = new();
        private readonly List<ProjectCard> _active = new();

        public System.Action<Project> OnProjectClicked;

        /// <summary>Fired when the "See All" button is tapped.</summary>
        public System.Action OnSeeAllClicked;

        public bool IsBound { get; private set; }

        private void Awake()
        {
            if (seeAllButton != null)
                seeAllButton.onClick.AddListener(() => OnSeeAllClicked?.Invoke());
        }

        public void SetTitle(string title)
        {
            titleText.text = title;
        }

        public void Bind(string title, IReadOnlyList<Project> projects)
        {
            titleText.text = title;
            IsBound = true;
            ReturnAll();

            foreach (var p in projects)
            {
                var card = GetCard();
                card.OnClicked = proj => OnProjectClicked?.Invoke(proj);
                card.Bind(p);
            }

            if (seeAllButton != null)
                seeAllButton.gameObject.SetActive(projects.Count > 0);
        }

        public void Clear() => ReturnAll();

        /// <summary>
        /// Cancels in-flight thumbnail requests on all active cards without pooling them.
        /// </summary>
        public void CancelAllLoads()
        {
            foreach (var c in _active)
                c.CancelLoads();
        }

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
                card = Instantiate(cardPrefab, slideContent);
            }
            card.gameObject.SetActive(true);
            card.transform.SetAsLastSibling();
            _active.Add(card);
            return card;
        }

        private void ReturnAll()
        {
            foreach (var c in _active)
            {
                c.CancelLoads();
                c.gameObject.SetActive(false);
                _pool.Add(c);
            }
            _active.Clear();
        }
    }
}
