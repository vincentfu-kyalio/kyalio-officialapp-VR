using UnityEngine;

namespace Kyalio.Components
{
    /// <summary>
    /// Sidebar convention shared by HomeMenuPanel, SeriesRolePanel and MyKyalioPage:
    /// a menu button shows it is selected by enabling its child named "SelectedIndicator".
    /// Buttons without that child are left unchanged.
    /// </summary>
    public static class SelectedIndicator
    {
        public const string ChildName = "SelectedIndicator";

        public static void Set(Component item, bool selected)
        {
            var indicator = item.transform.Find(ChildName);
            if (indicator != null)
                indicator.gameObject.SetActive(selected);
        }
    }
}
