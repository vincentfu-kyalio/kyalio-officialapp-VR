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

        private ScrollRect _scrollRect;
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

            // Rebind the cards already showing in place (CardPool.GetOrReuse).
            for (int i = 0; i < items.Count; i++)
            {
                var card = Pool.GetOrReuse(i);
                card.OnClicked = (projectId, _) => OnProjectClicked?.Invoke(projectId);
                card.Bind(items[i]);
            }
            Pool.TrimTo(items.Count);

            RefreshLayout();

            if (seeAllButton != null)
                seeAllButton.gameObject.SetActive(items.Count > 0);
        }

        public void Clear() => Pool.ReturnAll();

        /// <summary>
        /// The card grid's ContentSizeFitter sits inside the ScrollRect content's own fitter,
        /// and a single layout pass can size the outer one before the inner one — the
        /// ScrollRect then thinks nothing overflows (no scrollbar, stuck scroll). Rebuilding
        /// the whole content after binding settles both, as ProjectCardList does.
        /// </summary>
        private void RefreshLayout()
        {
            if (_scrollRect == null) _scrollRect = GetComponentInChildren<ScrollRect>(includeInactive: true);
            if (_scrollRect != null && _scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
        }
    }
}
