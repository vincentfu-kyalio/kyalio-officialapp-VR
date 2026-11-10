using System;
using Cysharp.Threading.Tasks;
using Kyalio.Components;
using Kyalio.State;
using Kyalio.Utils;
using UnityEngine;
using AppState = Kyalio.State.V2.AppState;

namespace Kyalio.Core
{
    /// <summary>
    /// Session-level operations shared across the app. Currently logout: tells the server
    /// to revoke the active Quest session, clears the bearer token, wipes in-memory session
    /// state, and returns to the pairing screen.
    ///
    /// Wire a logout button to <see cref="Logout"/>. Downloaded files are intentionally
    /// retained across logout (see DownloadedVideoState).
    /// </summary>
    public static class Session
    {
        /// <summary>Fire-and-forget logout for UI button handlers.</summary>
        public static void Logout() => LogoutAsync().Forget();

        public static async UniTaskVoid LogoutAsync()
        {
            // 1. Revoke the server session while the token is still attached.
            //    Always 204; never block teardown on its result.
            try
            {
                await ServiceLocator.Instance.V2.Auth.LogoutAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] Server logout failed (continuing): {e.Message}");
            }

            // 2. Drop the bearer and the persisted credential now (before returning to Login,
            //    or LoginPage would auto-resume it), then go back to Login through the black
            //    fade. In-memory state is cleared while the screen is dark, so thumbnails do
            //    not blank out on the still-visible main UI.
            AuthStorage.ClearCredential();
            ServiceLocator.Instance.ApiClient.ClearToken();
            ReturnToLogin();
        }

        private static void ClearSessionState()
        {
            AppState.Reset();          // also resets ProjectCacheRepository (V2)
            UserLocalState.Reset();
            PlaybackState.Reset();
            ThumbnailLoader.ClearCache();
        }

        // Back to the pairing screen, clearing navigation history; the tab bar is hidden by
        // LoginPage.OnEnter.
        private static void ReturnToLogin(Action thenOnLogin = null)
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                ClearSessionState();
                return;
            }
            ui.SwitchPage(PageType.Login, fade: true, alongside: ClearSessionState);
            thenOnLogin?.Invoke();
        }

        /// <summary>
        /// Handle access-token expiry (401). Skips the server-side logout call (the bearer is
        /// already invalid) and drops local session state before returning to the pairing
        /// screen. Quest has no refresh-token flow, so expiry always means re-pair. Shows a
        /// popup informing the user why they were signed out — the underlying page is switched
        /// to Login first so the popup overlays a stable screen when dismissed.
        /// </summary>
        public static void ExpireToLogin()
        {
            Debug.Log("[Session] Access token expired — returning to login.");

            AuthStorage.ClearCredential();
            ServiceLocator.Instance.ApiClient.ClearToken();

            // The popup opens at once and fades over the transition, so the reason for
            // being signed out is up before the main UI goes dark.
            ReturnToLogin(() =>
                PopupManager.Instance?.ShowDone("Your session has expired. Please sign in again."));
        }
    }
}
