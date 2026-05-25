using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Recently Watched horizontal scrolling section.
    /// Inspector: titleText, seeAllButton (optional), slideContent, cardPrefab
    /// </summary>
    public class RecentlyWatchedSection : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button seeAllButton;
        [SerializeField] private Transform slideContent;
        [SerializeField] private RecentlyWatchedCard cardPrefab;

        private readonly List<RecentlyWatchedCard> _pool   = new();
        private readonly List<RecentlyWatchedCard> _active = new();

        public System.Action<string> OnProjectClicked; // projectId
        public System.Action OnSeeAllClicked;

        private void Awake()
        {
            if (seeAllButton != null)
                seeAllButton.onClick.AddListener(() => OnSeeAllClicked?.Invoke());
        }

        public void Bind(string title, IReadOnlyList<WatchHistoryItem> items)
        {
            titleText.text = title;
            ReturnAll();

            foreach (var item in items)
            {
                var card = GetCard();
                card.OnClicked = (projectId, _) => OnProjectClicked?.Invoke(projectId);
                card.Bind(item);
            }

            if (seeAllButton != null)
                seeAllButton.gameObject.SetActive(items.Count > 0);
        }

        public void Clear() => ReturnAll();

        private RecentlyWatchedCard GetCard()
        {
            RecentlyWatchedCard card;
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
