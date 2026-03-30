using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Core;
using Kyalio.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Pages
{
    /// <summary>
    /// Overlay panel for entering a 6-digit VR pairing code (POST /api/pair/verify).
    /// Opened by HomePage's Auth VR button. The panel's root GameObject starts inactive.
    ///
    /// Inspector:
    ///   closeButton      — closes the overlay at any time
    ///   content          — panel containing codeInput, wrongMessage, continueButton
    ///   codeInput        — TMP_InputField for the 6-digit code
    ///   wrongMessage     — TMP text shown on failure (child of content)
    ///   continueButton   — submits the code
    ///   loading          — spinner/indicator shown while the request is in flight
    ///   successMessage   — shown on 204 success
    /// </summary>
    public class EnterCodePage : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button closeButton;

        [Header("Content")]
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private TextMeshProUGUI wrongMessage;
        [SerializeField] private Button continueButton;

        [Header("States")]
        [SerializeField] private GameObject loading;
        [SerializeField] private GameObject successMessage;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (continueButton != null)
                continueButton.onClick.AddListener(() => OnContinueClickedAsync().Forget());
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Shows the overlay and resets to the initial content state.</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            codeInput.text = string.Empty;
            ShowContent(showError: false);
        }

        /// <summary>Hides the overlay and cancels any in-flight request.</summary>
        public void Close()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            gameObject.SetActive(false);
        }

        // ── Continue ──────────────────────────────────────────────────

        private async UniTaskVoid OnContinueClickedAsync()
        {
            var code = codeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                ShowContent(showError: true, "Please enter the 6-digit code.");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            ShowLoading();

            try
            {
                await ServiceLocator.Instance.AuthService.VerifyPairCodeAsync(code, ct);

                // 204 — pairing successful
                if (!ct.IsCancellationRequested)
                    ShowSuccess();
            }
            catch (OperationCanceledException)
            {
                // Closed while request was in flight — do nothing.
            }
            catch (ApiException e) when (e.StatusCode == 404)
            {
                ShowContent(showError: true, "Code not found or expired.");
            }
            catch (ApiException e) when (e.StatusCode == 409)
            {
                ShowContent(showError: true, "Code already used.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnterCodePage] VerifyPairCode failed: {e.Message}");
                ShowContent(showError: true, "Something went wrong. Please try again.");
            }
        }

        // ── State helpers ─────────────────────────────────────────────

        private void ShowContent(bool showError, string errorText = "")
        {
            content.SetActive(true);
            loading.SetActive(false);
            successMessage.SetActive(false);

            if (wrongMessage != null)
            {
                wrongMessage.gameObject.SetActive(showError);
                if (showError)
                    wrongMessage.text = errorText;
            }
        }

        private void ShowLoading()
        {
            content.SetActive(false);
            loading.SetActive(true);
            successMessage.SetActive(false);
        }

        private void ShowSuccess()
        {
            content.SetActive(false);
            loading.SetActive(false);
            successMessage.SetActive(true);
        }
    }
}
