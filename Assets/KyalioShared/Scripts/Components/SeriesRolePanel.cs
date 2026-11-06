using System.Collections.Generic;
using Kyalio.Models.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// SeriesPage left sidebar: one Button per home-roles role.
    /// Uses the same "SelectedIndicator" child-GameObject convention as HomeMenuPanel.
    /// Inspector: menuItemPrefab, container
    /// </summary>
    public class SeriesRolePanel : MonoBehaviour
    {
        [SerializeField] private Button menuItemPrefab;
        [SerializeField] private Transform container;

        public event System.Action<HomeRoleItem> OnRoleSelected;

        /// <summary>Role selected last; kept across Build() so the page can restore it.</summary>
        public HomeRoleItem SelectedRole { get; private set; }

        private readonly List<(Button button, HomeRoleItem role)> _items = new();
        private Button _selectedButton;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Clears and rebuilds the role list.</summary>
        public void Build(List<HomeRoleItem> roles)
        {
            foreach (var (btn, _) in _items)
                if (btn != null) Destroy(btn.gameObject);
            _items.Clear();
            _selectedButton = null;

            if (roles == null) return;

            foreach (var role in roles)
                CreateItem(role);
        }

        /// <summary>
        /// Highlights the role named <paramref name="roleName"/> without firing OnRoleSelected
        /// (roles have no id; names are unique per home payload). Falls back to the first role
        /// when it no longer exists. Returns the selected role, or null when there are none.
        /// </summary>
        public HomeRoleItem Restore(string roleName)
        {
            if (_items.Count == 0) return null;

            var index = _items.FindIndex(item => item.role.Name == roleName);
            var (btn, role) = _items[index >= 0 ? index : 0];
            SetSelected(btn, role);
            return role;
        }

        // ── Private ───────────────────────────────────────────────────

        private void CreateItem(HomeRoleItem role)
        {
            var button = Instantiate(menuItemPrefab, container);

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = role.Name;

            SetSelectedVisual(button, false);
            button.onClick.AddListener(() => SelectItem(button, role));

            _items.Add((button, role));
        }

        private void SelectItem(Button button, HomeRoleItem role)
        {
            SetSelected(button, role);
            OnRoleSelected?.Invoke(role);
        }

        private void SetSelected(Button button, HomeRoleItem role)
        {
            if (_selectedButton != null)
                SetSelectedVisual(_selectedButton, false);

            _selectedButton = button;
            SetSelectedVisual(button, true);

            SelectedRole = role;
        }

        private static void SetSelectedVisual(Button button, bool selected)
        {
            var indicator = button.transform.Find("SelectedIndicator");
            if (indicator != null)
                indicator.gameObject.SetActive(selected);
        }
    }
}
