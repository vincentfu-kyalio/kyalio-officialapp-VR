using System.Collections.Generic;
using Kyalio.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Filter dropdown panel: a scrollable checklist of items + Done button.
    /// Open/close is controlled by the parent via SetActive.
    /// Start this GameObject as inactive in the Inspector.
    /// Inspector: itemTogglePrefab, itemContainer, doneButton
    /// </summary>
    public class FilterDropdownPanel : MonoBehaviour
    {
        [SerializeField] private Toggle itemTogglePrefab;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private Button doneButton;

        public event System.Action OnDone;

        private readonly List<(Toggle toggle, string id)> _items = new();

        private void Awake()
        {
            doneButton.onClick.AddListener(() => OnDone?.Invoke());
        }

        /// <summary>
        /// Rebuilds the checklist. Pre-checks items whose IDs are in selectedIds.
        /// </summary>
        public void Build(List<Category> categories, HashSet<string> selectedIds)
        {
            foreach (Transform child in itemContainer)
                Destroy(child.gameObject);
            _items.Clear();

            foreach (var cat in categories)
            {
                var toggle = Instantiate(itemTogglePrefab, itemContainer);
                var label = toggle.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = cat.Name;
                toggle.SetIsOnWithoutNotify(selectedIds.Contains(cat.Id));
                _items.Add((toggle, cat.Id));
            }
        }

        /// <summary>Returns the IDs of all currently checked items.</summary>
        public HashSet<string> GetSelectedIds()
        {
            var result = new HashSet<string>();
            foreach (var (toggle, id) in _items)
                if (toggle.isOn) result.Add(id);
            return result;
        }
    }
}
