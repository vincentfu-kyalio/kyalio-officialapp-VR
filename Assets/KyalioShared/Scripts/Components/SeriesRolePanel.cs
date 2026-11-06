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

        private List<HomeRoleItem> _builtRoles;
        private bool _built;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Clears and rebuilds the role list when it changed. The roles come from
        /// AppState.LastHome, which is only replaced on login, so re-entering the page
        /// with the same list is a no-op.
        /// </summary>
        public void Build(List<HomeRoleItem> roles)
        {
            if (_built && ReferenceEquals(roles, _builtRoles)) return;
            _builtRoles = roles;
            _built = true;

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

            SelectedIndicator.Set(button, false);
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
                SelectedIndicator.Set(_selectedButton, false);

            _selectedButton = button;
            SelectedIndicator.Set(button, true);

            SelectedRole = role;
        }
    }
}
