using Kyalio.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Components
{
    /// <summary>
    /// Persistent tab bar with five entries: Home, Search, Series, MyKyalio, Account.
    /// Home / Search / Series / MyKyalio share a ToggleGroup.
    /// Account opens the Account menu popup (signed-in account, Log out, Exit) and never
    /// stays selected.
    ///
    /// Inspector:
    ///   homeToggle, searchToggle, seriesToggle, myKyalioToggle — inside a ToggleGroup
    ///   accountToggle — standalone, not in the group (formerly the Exit App toggle)
    /// </summary>
    public class TabBar : MonoBehaviour
    {
        public static TabBar Instance { get; private set; }

        [Header("Navigation toggles (ToggleGroup)")]
        [SerializeField] private Toggle homeToggle;
        [SerializeField] private Toggle searchToggle;
        [SerializeField] private Toggle seriesToggle;
        [SerializeField] private Toggle myKyalioToggle;

        [Header("Account (standalone)")]
        [FormerlySerializedAs("exitAppToggle")]
        [SerializeField] private Toggle accountToggle;

        // Prevent navigation callbacks when updating toggle visuals programmatically
        private bool _suppressNavigation;
        private Toggle _activeToggle;

        private void Awake()
        {
            Instance = this;

            homeToggle.onValueChanged.AddListener(on      => { if (on) OnNavToggle(homeToggle,      PageType.Home); });
            searchToggle.onValueChanged.AddListener(on    => { if (on) OnNavToggle(searchToggle,    PageType.Search); });
            seriesToggle.onValueChanged.AddListener(on    => { if (on) OnNavToggle(seriesToggle,    PageType.Series); });
            myKyalioToggle.onValueChanged.AddListener(on  => { if (on) OnNavToggle(myKyalioToggle,  PageType.MyKyalio); });
            accountToggle.onValueChanged.AddListener(on   => { if (on) OnAccountToggled(); });
        }

        /// <summary>
        /// Update the tab bar selection to match the given page without triggering navigation.
        /// Call this when navigating to a top-level page from code (e.g. after login).
        /// </summary>
        public void SelectTab(PageType pageType)
        {
            var target = ToggleForPage(pageType);
            if (target == null || target == _activeToggle) return;

            _suppressNavigation = true;
            target.SetIsOnWithoutNotify(true);
            if (_activeToggle != null) _activeToggle.SetIsOnWithoutNotify(false);
            _activeToggle = target;
            _suppressNavigation = false;
        }

        // ── Private ───────────────────────────────────────────────────

        private void OnNavToggle(Toggle toggle, PageType pageType)
        {
            _activeToggle = toggle;
            if (_suppressNavigation) return;
            UIManager.Instance.SwitchPage(pageType);
        }

        private void OnAccountToggled()
        {
            // Restore previous tab immediately — Account never stays selected
            _suppressNavigation = true;
            accountToggle.SetIsOnWithoutNotify(false);
            if (_activeToggle != null) _activeToggle.SetIsOnWithoutNotify(true);
            _suppressNavigation = false;

            var account = AppState.Instance.Account;
            PopupManager.Instance.ShowAccount(
                AccountMessage(account),
                onLogout: ConfirmLogout,
                // Choosing Exit in the menu is already deliberate; no second confirmation.
                onExit: Application.Quit);

            // Not fetched yet (e.g. the fetch after login failed): fetch now and fill in
            // the open menu when it arrives.
            if (account == null) FillAccountWhenFetchedAsync().Forget();
        }

        private static async Cysharp.Threading.Tasks.UniTaskVoid FillAccountWhenFetchedAsync()
        {
            try
            {
                var account = await MyKyalioLoader.FetchAccountAsync();
                PopupManager.Instance?.UpdateAccountMessage(AccountMessage(account));
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[TabBar] Account fetch failed: {e.Message}"); }
        }

        // "Signed in as Name (email)", or the email alone when no display name is set.
        private static string AccountMessage(Kyalio.Models.V2.MeResponse account)
        {
            if (account == null || string.IsNullOrEmpty(account.Email))
                return "Signed in";
            return string.IsNullOrEmpty(account.Name)
                ? $"Signed in as\n{account.Email}"
                : $"Signed in as\n{account.Name}\n<size=80%>{account.Email}</size>";
        }

        private static void ConfirmLogout()
        {
            PopupManager.Instance.ShowYesNo(
                "Log out of this account?\nYou will need to sign in again. " +
                "Downloaded videos stay on this headset.",
                onYes: Session.Logout);
        }

        private Toggle ToggleForPage(PageType pageType) => pageType switch
        {
            PageType.Home      => homeToggle,
            PageType.Search    => searchToggle,
            PageType.Series    => seriesToggle,
            PageType.MyKyalio  => myKyalioToggle,
            _                  => null
        };
    }
}
