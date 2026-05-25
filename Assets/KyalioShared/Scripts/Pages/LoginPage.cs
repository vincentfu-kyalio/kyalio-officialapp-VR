using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Services;
using Kyalio.State.V2;
using TMPro;
using UnityEngine;

namespace Kyalio.Pages
{
    /// <summary>
    /// Quest pairing login page.
    ///
    /// Flow:
    ///   1. OnEnter → POST /api/pair/request → display 6-digit code across the six digit labels.
    ///   2. Open SSE connection to GET /api/pair/stream/{code} and wait for a terminal event.
    ///   3. On "verified" → store accessToken in ApiClient, then run the V2 sync triad:
    ///        - GET  /api/me/sync               (granted-project versions)
    ///        - POST /api/projects/batch        (missing + outdated)
    ///        - GET  /api/me/home               (home layout + filters)
    ///        - GET  /api/me/progress           (full progress snapshot)
    ///   4. Popup Done → activate TabBar, navigate to Home.
    ///   5. On "expired" or 404 → automatically request a new code.
    ///
    /// API config (base URL + Quest key + app version) is set once on AppBootstrapper
    /// and stored in ServiceLocator — this page reads V2 services directly from there.
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

            // Pairing screen must stand alone — hide the tab bar (fresh boot or after logout).
            if (_tabBarRoot != null)
                _tabBarRoot.SetActive(false);

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
                var response = await ServiceLocator.Instance.V2.Auth
                    .RequestPairAsync(ct);

                DisplayCode(response.Code);
                SetLoading(false);
                await StreamUntilVerifiedAsync(response.Code, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPage] Pair request failed: {e.Message}");
                SetLoading(false);
            }
        }

        private async UniTask StreamUntilVerifiedAsync(string code, CancellationToken ct)
        {
            PairStreamPayload result;
            try
            {
                result = await ServiceLocator.Instance.V2.Auth
                    .StreamPairAsync(code, ct);
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                Debug.Log("[LoginPage] Pair code not found; requesting a new code.");
                ClearDigits();
                SetLoading(true);
                StartPairingAsync(ct).Forget();
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoginPage] SSE error: {e.Message}");
                return;
            }

            Debug.Log($"[LoginPage] SSE status: '{result?.Status ?? "(null)"}'");

            if (string.Equals(result?.Status, "verified", StringComparison.OrdinalIgnoreCase))
            {
                if (result.Credential == null || string.IsNullOrEmpty(result.Credential.AccessToken))
                {
                    Debug.LogError("[LoginPage] Verified response is missing accessToken — aborting.");
                    SetLoading(false);
                    return;
                }
                OnPairingVerified(result.Credential);
                return;
            }

            if (string.Equals(result?.Status, "expired", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[LoginPage] Pair code expired; requesting a new code.");
                ClearDigits();
                SetLoading(true);
                StartPairingAsync(ct).Forget();
            }
        }

        private void OnPairingVerified(Credential credential)
        {
            Debug.Log("[LoginPage] Pairing verified — setting token.");
            ServiceLocator.Instance.ApiClient.SetToken(credential.AccessToken);
            AppState.Instance.SetLoggedIn();
            BootstrapSessionAndProceedAsync(_cts.Token).Forget();
        }

        // ── Sync triad: sync → batch → home → progress ────────────────

        private async UniTaskVoid BootstrapSessionAndProceedAsync(CancellationToken ct)
        {
            SetLoading(true);

            try
            {
                var v2 = ServiceLocator.Instance.V2;
                var repo = ProjectCacheRepository.Instance;

                // 1. /me/sync — authoritative granted set.
                Debug.Log("[LoginPage] /api/me/sync …");
                var sync = await v2.Sync.SyncAsync(ct);
                if (ct.IsCancellationRequested) return;
                var diff = repo.ApplySync(sync);

                // 2. /projects/batch for missing + version-outdated projects.
                if (diff.Missing.Count + diff.OutdatedProject.Count > 0)
                {
                    Debug.Log($"[LoginPage] /api/projects/batch ({diff.Missing.Count} missing + " +
                              $"{diff.OutdatedProject.Count} outdated)");
                    var batch = await v2.Content.BatchAsync(diff.ToBatch, ct);
                    if (ct.IsCancellationRequested) return;
                    repo.ApplyBatch(batch);
                }

                // 3. /me/home — layout + normalized specialty / program catalog.
                Debug.Log("[LoginPage] /api/me/home …");
                var home = await v2.Home.GetHomeAsync(ct);
                if (ct.IsCancellationRequested) return;
                AppState.Instance.SetHome(home);

                // 4. /me/progress — full snapshot on cold start.
                Debug.Log("[LoginPage] /api/me/progress …");
                var progress = await v2.Sync.GetProgressAsync(since: null, ct: ct);
                if (ct.IsCancellationRequested) return;
                repo.ApplyProgress(progress, merge: false);

                Debug.Log($"[LoginPage] Session bootstrap complete — {repo.All.Count} projects cached, " +
                          $"{repo.Specialties.Count} specialties, {repo.Programs.Count} programs.");
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoginPage] Session bootstrap failed: {e.Message}");
                // Non-fatal: proceed even if any of the sync calls fails.
            }

            ShowPairedPopupAndProceed();
        }

        private void ShowPairedPopupAndProceed()
        {
            Debug.Log("[LoginPage] Showing paired popup.");

            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.ShowDone("Paired successfully!", onDone: NavigateToHome);
            }
            else
            {
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
