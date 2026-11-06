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

        private CardPool<RecentlyWatchedCard> _pool;
        private CardPool<RecentlyWatchedCard> Pool => _pool ??= new(cardPrefab, slideContent, c => c.CancelLoads());

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
            Pool.ReturnAll();

            foreach (var item in items)
            {
                var card = Pool.Get();
                card.OnClicked = (projectId, _) => OnProjectClicked?.Invoke(projectId);
                card.Bind(item);
            }

            if (seeAllButton != null)
                seeAllButton.gameObject.SetActive(items.Count > 0);
        }

        public void Clear() => Pool.ReturnAll();
    }
}
