using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Horizontal chip row: "Clear All" button + one chip button per selected filter item.
    /// Automatically hides the entire GameObject when no items are selected.
    /// Inspector: clearAllButton, chipPrefab, chipContainer
    /// </summary>
    public class FilterChipRow : MonoBehaviour
    {
        [SerializeField] private Button clearAllButton;
        [SerializeField] private Button chipPrefab;
        [SerializeField] private Transform chipContainer;

        /// <summary>Fired when the Clear All button is tapped.</summary>
        public event System.Action OnClearAll;

        /// <summary>Fired when a chip is tapped; passes the item's ID.</summary>
        public event System.Action<string> OnChipClicked;

        // Chips are reused (CardPool.GetOrReuse) instead of Destroy / Instantiate per Bind.
        private CardPool<Button> _pool;
        private CardPool<Button> Pool => _pool ??= new(chipPrefab, chipContainer);

        // Id each pooled chip stands for; its single click listener reads it from here.
        private readonly Dictionary<Button, string> _chipIds = new();

        private void Awake()
        {
            clearAllButton.onClick.AddListener(() => OnClearAll?.Invoke());
        }

        /// <summary>
        /// Rebuilds the chip list from allItems filtered by selectedIds.
        /// Hides the parent GameObject when selectedIds is empty.
        /// </summary>
        public void Bind(List<IdNameRef> allItems, HashSet<string> selectedIds)
        {
            int count = 0;
            foreach (var item in allItems)
            {
                if (!selectedIds.Contains(item.Id)) continue;

                var chip = Pool.GetOrReuse(count++);
                var label = chip.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{item.Name} <sprite=0>";

                if (!_chipIds.ContainsKey(chip))
                    chip.onClick.AddListener(() => OnChipClicked?.Invoke(_chipIds[chip]));
                _chipIds[chip] = item.Id;
            }
            Pool.TrimTo(count);

            gameObject.SetActive(selectedIds.Count > 0);
        }
    }
}
