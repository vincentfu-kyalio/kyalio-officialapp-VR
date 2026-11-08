using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.Core;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.Services;
using Kyalio.State.V2;
using Kyalio.Utils;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Quest login page with two sign-in methods (both yield the same Quest credential):
    ///
    ///   • Password (default) — POST /api/auth/quest/login. A 409 means signing in would end
    ///     another Quest session: confirm via popup → POST /api/auth/quest/login/confirm
    ///     (which can itself return a new 409 + ticket; loop until resolved or cancelled).
    ///   • Code — POST /api/pair/request → show 6 digits → SSE GET /api/pair/stream/{code}.
    ///     Pairing only runs while the code panel is visible.
    ///
    /// Resume: when _resumeStoredSession is on, a stored, unexpired credential (AuthStorage)
    /// skips the form. Off by default — every launch requires signing in, and any credential
    /// left by an older build is cleared. Remember Me still pre-fills the form either way.
    /// After any successful sign-in the V2 sync triad runs (sync → batch → home → progress),
    /// then the page navigates to Home. Errors are shown through PopupManager.
    /// </summary>
    public class LoginPage : MonoBehaviour, IPageHandler
    {
        private enum Mode { Password, Code }

        [Header("Panels")]
        [SerializeField] private GameObject _usePasswordPanel;
        [SerializeField] private GameObject _useCodePanel;

        [Header("Password login")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private Toggle _showPasswordToggle;
        [SerializeField] private Toggle _rememberToggle;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _forgotPasswordButton;
        [Tooltip("\"use code instead\" button on the password panel.")]
        [SerializeField] private Button _useCodeButton;

        [Header("Code pairing")]
        [SerializeField] private TextMeshProUGUI[] _codeDigits = new TextMeshProUGUI[6];
        [Tooltip("\"use password instead\" button on the code panel.")]
        [SerializeField] private Button _usePasswordButton;

        [Header("Shared")]
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _tabBarRoot;

        [Header("Session")]
        [Tooltip("Skip the login form on launch while a stored credential is still valid.")]
        [SerializeField] private bool _resumeStoredSession = false;

        private CancellationTokenSource _cts;      // page lifetime
        private CancellationTokenSource _pairCts;  // code-mode lifetime (child of _cts)
        private bool _busy;

        // ── Unity ─────────────────────────────────────────────────────

        private void Awake()
        {
            // Explicit != null: unassigned serialized fields are Unity "fake null", which ?. misses.
            if (_loginButton != null)          _loginButton.onClick.AddListener(OnLoginClicked);
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);
            if (_useCodeButton != null)        _useCodeButton.onClick.AddListener(() => SetMode(Mode.Code));
            if (_usePasswordButton != null)    _usePasswordButton.onClick.AddListener(() => SetMode(Mode.Password));
            if (_showPasswordToggle != null)   _showPasswordToggle.onValueChanged.AddListener(ApplyPasswordVisibility);
            if (_rememberToggle != null)       _rememberToggle.onValueChanged.AddListener(OnRememberChanged);
            SystemKeyboardInput.EnsureOn(_emailInput);
            SystemKeyboardInput.EnsureOn(_passwordInput);

            // No onSubmit → login: the Quest system keyboard reports Status.Done on any
            // close (checkmark or hide), and TMP turns Done into onSubmit, so dismissing
            // the keyboard would sign in. Login is triggered only by the Login button.
        }

        // ── IPageHandler ──────────────────────────────────────────────

        public void OnEnter(object param)
        {
            CancelAll();
            _cts = new CancellationTokenSource();
            _busy = false;

            // Login screen must stand alone — hide the tab bar (fresh boot or after logout).
            if (_tabBarRoot != null)
                _tabBarRoot.SetActive(false);

            if (!_resumeStoredSession)
            {
                AuthStorage.ClearCredential();
            }
            else if (AuthStorage.TryGetValidToken(out var storedToken))
            {
                ResumeStoredSession(storedToken);
                return;
            }

            LoadRememberedFields();
            SetMode(Mode.Password);
        }

        public void OnExit()
        {
            CancelAll();
            SetBusy(false);
        }

        private void CancelAll()
        {
            StopPairing();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ── Mode switching ────────────────────────────────────────────

        private void SetMode(Mode mode)
        {
            if (_busy) return;

            if (_usePasswordPanel != null) _usePasswordPanel.SetActive(mode == Mode.Password);
            if (_useCodePanel != null)     _useCodePanel.SetActive(mode == Mode.Code);

            StopPairing();
            SetLoading(false);

            if (mode == Mode.Code)
            {
                _pairCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                ClearDigits();
                SetLoading(true);
                StartPairingAsync(_pairCts.Token).Forget();
            }
        }

        private void StopPairing()
        {
            _pairCts?.Cancel();
            _pairCts?.Dispose();
            _pairCts = null;
        }

        // ── Resume stored session ─────────────────────────────────────

        private void ResumeStoredSession(string token)
        {
            Debug.Log("[LoginPage] Resuming stored Quest credential.");
            if (_usePasswordPanel != null) _usePasswordPanel.SetActive(false);
            if (_useCodePanel != null)     _useCodePanel.SetActive(false);

            ServiceLocator.Instance.ApiClient.SetToken(token);
            AppState.Instance.SetLoggedIn();
            // A revoked token surfaces as 401 during the sync → Session.ExpireToLogin()
            // clears storage and re-enters this page, which cancels this bootstrap.
            BootstrapSessionAndProceedAsync(successMessage: null, _cts.Token).Forget();
        }

        // ── Password login ────────────────────────────────────────────

        private void OnLoginClicked()
        {
            if (_busy || _cts == null) return;

            var email = _emailInput != null ? _emailInput.text.Trim() : "";
            var password = _passwordInput != null ? _passwordInput.text : "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your email and password.");
                return;
            }
            if (!email.Contains("@"))
            {
                ShowError("Please enter a valid email address.");
                return;
            }

            PasswordLoginAsync(email, password, _cts.Token).Forget();
        }

        private async UniTaskVoid PasswordLoginAsync(string email, string password, CancellationToken ct)
        {
            SetBusy(true);
            try
            {
                var response = await ServiceLocator.Instance.V2.Auth
                    .QuestLoginAsync(email, password, ct);
                OnPasswordLoginSucceeded(response?.Credential, email, password, ct);
            }
            catch (OperationCanceledException) { }
            catch (ApiException ex) when (ex.StatusCode == 409)
            {
                SetBusy(false);
                PromptDeviceConflict(ex.Message, email, password, ct);
            }
            catch (Exception e)
            {
                SetBusy(false);
                ShowApiError(e);
            }
        }

        private void PromptDeviceConflict(string body, string email, string password, CancellationToken ct)
        {
            DeviceConflictResponse conflict = null;
            try { conflict = JsonConvert.DeserializeObject<DeviceConflictResponse>(body); }
            catch (Exception e) { Debug.LogWarning($"[LoginPage] 409 body parse failed: {e.Message}"); }

            if (string.IsNullOrEmpty(conflict?.Ticket))
            {
                ShowError("Sign-in could not be completed. Please try again.");
                return;
            }

            var ticket = conflict.Ticket;
            var message = BuildConflictMessage(conflict);
            if (PopupManager.Instance == null)
            {
                Debug.LogWarning("[LoginPage] PopupManager missing; cannot confirm device conflict.");
                return;
            }
            // "No" simply discards the ticket — nothing changed on the server.
            PopupManager.Instance.ShowYesNo(message,
                onYes: () => ConfirmLoginAsync(ticket, email, password, ct).Forget());
        }

        private async UniTaskVoid ConfirmLoginAsync(string ticket, string email, string password, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;
            SetBusy(true);
            try
            {
                var response = await ServiceLocator.Instance.V2.Auth
                    .ConfirmQuestLoginAsync(ticket, ct);
                OnPasswordLoginSucceeded(response?.Credential, email, password, ct);
            }
            catch (OperationCanceledException) { }
            catch (ApiException ex) when (ex.StatusCode == 409)
            {
                // A new conflict appeared after the first ticket — confirm again.
                SetBusy(false);
                PromptDeviceConflict(ex.Message, email, password, ct);
            }
            catch (Exception e)
            {
                SetBusy(false);
                ShowApiError(e);
            }
        }

        private void OnPasswordLoginSucceeded(Credential credential, string email, string password, CancellationToken ct)
        {
            var remember = _rememberToggle == null || _rememberToggle.isOn;
            AuthStorage.SaveRemembered(remember, email, password);
            OnCredentialReceived(credential, "Signed in successfully!", ct);
        }

        // ── Forgot password ───────────────────────────────────────────

        private void OnForgotPasswordClicked()
        {
            if (_busy || _cts == null) return;

            var email = _emailInput != null ? _emailInput.text.Trim() : "";
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                ShowError("Enter your email address above, then tap Forgot Password again.");
                return;
            }
            ForgotPasswordAsync(email, _cts.Token).Forget();
        }

        private async UniTaskVoid ForgotPasswordAsync(string email, CancellationToken ct)
        {
            SetBusy(true);
            try
            {
                await ServiceLocator.Instance.V2.Auth.ForgotPasswordAsync(email, ct);
                SetBusy(false);
                PopupManager.Instance?.ShowDone(
                    "If the account exists, a password reset email has been sent. " +
                    "Reset your password in a browser, then sign in here with the new password.");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                SetBusy(false);
                ShowApiError(e);
            }
        }

        // ── Code pairing ──────────────────────────────────────────────

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

            if (ct.IsCancellationRequested) return;
            Debug.Log($"[LoginPage] SSE status: '{result?.Status ?? "(null)"}'");

            if (string.Equals(result?.Status, "verified", StringComparison.OrdinalIgnoreCase))
            {
                if (result.Credential == null || string.IsNullOrEmpty(result.Credential.AccessToken))
                {
                    Debug.LogError("[LoginPage] Verified response is missing accessToken — aborting.");
                    SetLoading(false);
                    return;
                }
                _busy = true; // lock mode switching while the session bootstraps
                OnCredentialReceived(result.Credential, "Paired successfully!", _cts.Token);
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

        // ── Shared: credential → sync triad → Home ────────────────────

        private void OnCredentialReceived(Credential credential, string successMessage, CancellationToken ct)
        {
            if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
            {
                SetBusy(false);
                ShowError("Sign-in failed: the server returned no credential.");
                return;
            }

            Debug.Log("[LoginPage] Credential received — setting token.");
            StopPairing();
            ServiceLocator.Instance.ApiClient.SetToken(credential.AccessToken);
            if (_resumeStoredSession)
                AuthStorage.SaveCredential(credential);
            AppState.Instance.SetLoggedIn();
            BootstrapSessionAndProceedAsync(successMessage, ct).Forget();
        }

        private async UniTaskVoid BootstrapSessionAndProceedAsync(string successMessage, CancellationToken ct)
        {
            SetBusy(true);

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
                // Non-fatal: proceed even if any of the sync calls fails — unless the failure
                // was a 401, which already sent us back to Login (cancelling ct, dropping token).
            }

            if (ct.IsCancellationRequested || string.IsNullOrEmpty(ServiceLocator.Instance.ApiClient.Token))
                return;

            SetBusy(false);

            if (string.IsNullOrEmpty(successMessage) || PopupManager.Instance == null)
            {
                NavigateToHome();
                return;
            }
            PopupManager.Instance.ShowDone(successMessage, onDone: NavigateToHome);
        }

        private void NavigateToHome()
        {
            Debug.Log("[LoginPage] Navigating to Home.");
            if (_tabBarRoot != null)
                _tabBarRoot.SetActive(true);

            UIManager.Instance.SwitchPage(PageType.Home);
            TabBar.Instance?.SelectTab(PageType.Home);
        }

        // ── Remember Me / password visibility ─────────────────────────

        private void LoadRememberedFields()
        {
            if (AuthStorage.TryLoadRemembered(out var remember, out var email, out var password))
            {
                if (_rememberToggle != null) _rememberToggle.SetIsOnWithoutNotify(remember);
                if (_emailInput != null)    _emailInput.text    = remember ? email : "";
                if (_passwordInput != null) _passwordInput.text = remember ? password : "";
            }
            ApplyPasswordVisibility(_showPasswordToggle != null && _showPasswordToggle.isOn);
        }

        private void OnRememberChanged(bool remember)
        {
            // Unticking forgets saved credentials right away; ticking saves on next sign-in.
            if (!remember)
                AuthStorage.SaveRemembered(false, null, null);
        }

        private void ApplyPasswordVisibility(bool show)
        {
            if (_passwordInput == null) return;
            _passwordInput.contentType = show
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            _passwordInput.ForceLabelUpdate();
        }

        // ── Errors ────────────────────────────────────────────────────

        private static string BuildConflictMessage(DeviceConflictResponse c)
        {
            switch (c.Error?.Code)
            {
                case "DEVICE_REPLACE_REQUIRED":
                    return "This account is signed in on another Quest" + DescribeDevice(c.ExistingDevice) +
                           ".\nSign in here and sign out that headset?";
                case "DEVICE_ACCOUNT_SWITCH_REQUIRED":
                    return "This Quest is signed in to another account" + DescribeDevice(c.ExistingDevice) +
                           ".\nSign out that account and continue?";
                case "DEVICE_REPLACE_SWITCH_REQUIRED":
                    return "This account is signed in on another Quest" + DescribeDevice(c.ReplaceDevice) +
                           ", and this Quest is signed in to another account.\nSign out both and continue?";
                default:
                    return (c.Error?.Message ?? "Signing in will end another active session.") + "\nContinue?";
            }
        }

        private static string DescribeDevice(DeviceSummary d)
        {
            if (d == null) return "";
            var parts = d.DeviceModelCode;
            if (DateTime.TryParse(d.StartedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var started))
            {
                var since = started.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                parts = string.IsNullOrEmpty(parts) ? $"since {since}" : $"{parts}, since {since}";
            }
            return string.IsNullOrEmpty(parts) ? "" : $" ({parts})";
        }

        private void ShowApiError(Exception e)
        {
            if (e is ApiException api)
            {
                // 426 → ApiClient already raised the force-update overlay.
                if (api.StatusCode == 426) return;

                string code = null;
                try { code = JsonConvert.DeserializeObject<ErrorResponse>(api.Message)?.Error?.Code; }
                catch { /* non-JSON body */ }

                Debug.LogWarning($"[LoginPage] Auth error {api.StatusCode} {code}");
                ShowError(code switch
                {
                    "AUTH_INVALID_CREDENTIALS" => "Incorrect email or password.",
                    "AUTH_MISSING_CREDENTIALS" => "Please enter your email and password.",
                    "AUTH_RATE_LIMITED"        => "Too many attempts. Please wait a while and try again.",
                    "DEVICE_TICKET_INVALID"    => "The confirmation timed out. Please sign in again.",
                    "AUTH_SERVICE_ERROR"       => "The sign-in service is temporarily unavailable. Please try again later.",
                    _ when api.StatusCode == 429 => "Too many attempts. Please wait a while and try again.",
                    _ when api.StatusCode >= 500 => "The server is temporarily unavailable. Please try again later.",
                    _ when api.StatusCode == 0   => "Cannot connect to the server. Check your network connection.",
                    _ => "Something went wrong. Please try again.",
                });
                return;
            }

            Debug.LogWarning($"[LoginPage] Auth error: {e.Message}");
            ShowError("Cannot connect to the server. Check your network connection.");
        }

        private static void ShowError(string message)
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowDone(message);
            else
                Debug.LogWarning($"[LoginPage] {message}");
        }

        // ── UI helpers ────────────────────────────────────────────────

        private void SetBusy(bool busy)
        {
            _busy = busy;
            SetLoading(busy);
            if (_loginButton != null)          _loginButton.interactable = !busy;
            if (_forgotPasswordButton != null) _forgotPasswordButton.interactable = !busy;
            if (_useCodeButton != null)        _useCodeButton.interactable = !busy;
            if (_usePasswordButton != null)    _usePasswordButton.interactable = !busy;
        }

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
