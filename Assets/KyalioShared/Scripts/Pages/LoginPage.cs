using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.Services;
using Kyalio.State;
using TMPro;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Quest pairing login page.
    ///
    /// Flow:
    ///   1. OnEnter → POST /api/pair/request → display 6-digit code across the six digit labels.
    ///   2. Poll GET /api/pair/poll/{code} every 3 seconds.
    ///   3. On "verified" → store Bearer token in ApiClient, show "Paired successfully!" popup.
    ///   4. Popup Done → activate TabBar, navigate to Home.
    ///   5. On 404 (code expired) → automatically request a new code.
    ///
    /// API config (base URL + Quest key) is set once on AppBootstrapper and stored in
    /// ServiceLocator — this page reads QuestPairingService directly from there.
    ///
    /// Inspector:
    ///   _codeDigits      — exactly 6 TextMeshProUGUI components, one per digit (left to right)
    ///   _loadingIndicator — (optional) shown while the pair/request is in flight
    ///   _tabBarRoot      — root GameObject of the TabBar; hidden until login succeeds
    /// </summary>
    public class LoginPage : MonoBehaviour, IPageHandler
    {
        [SerializeField] private TextMeshProUGUI[] _codeDigits = new TextMeshProUGUI[6];
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _tabBarRoot;

        private CancellationTokenSource _cts;

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            ClearDigits();
            SetLoading(true);
            StartPairingAsync(_cts.Token).Forget();
        }

        public void OnExit()
        {
            _cts?.Cancel();
        }

        // ── Pairing flow ──────────────────────────────────────────────

        private async UniTaskVoid StartPairingAsync(CancellationToken ct)
        {
            try
            {
                var response = await ServiceLocator.Instance.QuestPairingService
                    .RequestPairAsync(ct);

                DisplayCode(response.Code);
                SetLoading(false);
                await PollUntilVerifiedAsync(response.Code, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPage] Pair request failed: {e.Message}");
                SetLoading(false);
            }
        }

        private async UniTask PollUntilVerifiedAsync(string code, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(3000, cancellationToken: ct);

                PairPollResponse poll;
                try
                {
                    poll = await ServiceLocator.Instance.QuestPairingService
                        .PollAsync(code, ct);
                }
                catch (ApiException ex) when (ex.StatusCode == 404)
                {
                    // Code expired — request a fresh one
                    Debug.Log("[LoginPage] Pair code expired; requesting a new code.");
                    ClearDigits();
                    SetLoading(true);
                    StartPairingAsync(ct).Forget();
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LoginPage] Poll error: {e.Message}");
                    continue;
                }

                Debug.Log($"[LoginPage] Poll status: '{poll?.Status ?? "(null response)"}'");

                if (poll == null)
                {
                    Debug.LogWarning("[LoginPage] Poll returned a null response body.");
                    continue;
                }

                if (string.Equals(poll.Status, "verified", StringComparison.OrdinalIgnoreCase))
                {
                    if (poll.Credential == null)
                    {
                        // Treat a verified-but-no-credential response as a fatal pairing
                        // failure — stop polling and show an error rather than looping.
                        Debug.LogError("[LoginPage] Verified response is missing credential — aborting.");
                        SetLoading(false);
                        return;
                    }
                    OnPairingVerified(poll.Credential);
                    return;
                }
            }
        }

        private void OnPairingVerified(PairPollCredential credential)
        {
            Debug.Log("[LoginPage] Pairing verified — setting token.");
            ServiceLocator.Instance.ApiClient.SetToken(credential.Token);
            AppState.Instance.SetLoggedIn();

            // Load subscriptions before navigating — populates ProjectCacheRepository
            // so every page (Search, Home, Series) has data ready on first enter.
            LoadSubscriptionsAndProceedAsync().Forget();
        }

        private async UniTaskVoid LoadSubscriptionsAndProceedAsync()
        {
            try
            {
                Debug.Log("[LoginPage] Loading subscriptions...");
                var subs = await ServiceLocator.Instance.AuthService
                    .GetSubscriptionsAsync();
                AppState.Instance.SetSubscriptions(subs?.Items);
                Debug.Log("[LoginPage] Subscriptions loaded.");
            }
            catch (Exception e)
            {
                // Non-fatal: proceed even if subscriptions fail to load.
                Debug.LogWarning($"[LoginPage] Subscriptions load failed: {e.Message}");
            }

            Debug.Log("[LoginPage] Showing paired popup.");

            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.ShowDone("Paired successfully!", onDone: NavigateToHome);
            }
            else
            {
                // PopupManager not available — navigate directly.
                Debug.LogWarning("[LoginPage] PopupManager not found; navigating directly.");
                NavigateToHome();
            }
        }

        private void NavigateToHome()
        {
            Debug.Log("[LoginPage] Navigating to Home.");
            if (_tabBarRoot != null)
                _tabBarRoot.SetActive(true);

            UIManager.Instance.SwitchPage(PageType.Home);
            TabBar.Instance?.SelectTab(PageType.Home);
        }

        // ── UI helpers ────────────────────────────────────────────────

        private void DisplayCode(string code)
        {
            ClearDigits();
            for (int i = 0; i < _codeDigits.Length && i < code.Length; i++)
            {
                if (_codeDigits[i] != null)
                    _codeDigits[i].text = code[i].ToString();
            }
        }

        private void ClearDigits()
        {
            foreach (var digit in _codeDigits)
                if (digit != null)
                    digit.text = "-";
        }

        private void SetLoading(bool isLoading)
        {
            if (_loadingIndicator != null)
                _loadingIndicator.SetActive(isLoading);
        }
    }
}
