using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Recently Watched card grid, virtualised (see <see cref="VirtualGrid{T}"/>).
    /// Inspector: titleText, seeAllButton (optional), slideContent (the GridLayoutGroup), cardPrefab
    /// </summary>
    public class RecentlyWatchedSection : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button seeAllButton;
        [SerializeField] private Transform slideContent;
        [SerializeField] private RecentlyWatchedCard cardPrefab;

        [Tooltip("Extra rows kept bound above and below the viewport.")]
        [SerializeField, Min(0)] private int bufferRows = 1;

        public System.Action<string> OnProjectClicked; // projectId
        public System.Action OnSeeAllClicked;

        private readonly List<WatchHistoryItem> _items = new();
        private VirtualGrid<RecentlyWatchedCard> _grid;

        // Created lazily: the page may bind before this component's Awake has run.
        // SetCount also rebuilds the scroll content, which settles the grid's
        // ContentSizeFitter nested inside the content's own fitter.
        private VirtualGrid<RecentlyWatchedCard> Grid => _grid ??= new VirtualGrid<RecentlyWatchedCard>(
            (RectTransform)slideContent, cardPrefab, null, bufferRows,
            onCreate: card => card.OnClicked = (projectId, _) => OnProjectClicked?.Invoke(projectId),
            bind: (card, i) => card.Bind(_items[i]),
            onHide: card => card.CancelLoads());

        private void Awake()
        {
            if (seeAllButton != null)
                seeAllButton.onClick.AddListener(() => OnSeeAllClicked?.Invoke());
        }

        public void Bind(string title, IReadOnlyList<WatchHistoryItem> items)
        {
            titleText.text = title;

            _items.Clear();
            if (items != null) _items.AddRange(items);
            Grid.SetCount(_items.Count, keepScroll: true);   // panels keep their scroll

            if (seeAllButton != null)
                seeAllButton.gameObject.SetActive(_items.Count > 0);
        }

        public void Clear()
        {
            _items.Clear();
            _grid?.SetCount(0, keepScroll: true);
        }

        private void OnDestroy() => _grid?.Dispose();
    }
}
