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

        private readonly List<Button> _chips = new();

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
            foreach (var chip in _chips)
                Destroy(chip.gameObject);
            _chips.Clear();

            foreach (var item in allItems)
            {
                if (!selectedIds.Contains(item.Id)) continue;

                var chip = Instantiate(chipPrefab, chipContainer);
                var label = chip.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{item.Name} <sprite=0>";

                var capturedId = item.Id;
                chip.onClick.AddListener(() => OnChipClicked?.Invoke(capturedId));
                _chips.Add(chip);
            }

            gameObject.SetActive(selectedIds.Count > 0);
        }
    }
}
