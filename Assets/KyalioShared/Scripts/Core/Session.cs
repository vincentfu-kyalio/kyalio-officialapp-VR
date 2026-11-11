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
            ExpiryDeferred = false;    // a manual logout also settles a pending expiry
            PlaybackActive = false;
            AppState.Reset();          // also resets ProjectCacheRepository (V2)
            UserLocalState.Reset();
            PlaybackState.Reset();
            ThumbnailLoader.ClearCache();
        }

        // Back to the pairing screen, clearing navigation history; the tab bar is hidden by
        // LoginPage.OnEnter.
        // thenOnLogin runs once Login is up (behind the black when fading).
        private static void ReturnToLogin(Action thenOnLogin = null)
        {
            var ui = UIManager.Instance;
            if (ui == null)
            {
                ClearSessionState();
                thenOnLogin?.Invoke();
                return;
            }
            ui.SwitchPage(PageType.Login, fade: true, alongside: ClearSessionState,
                afterSwitch: thenOnLogin);
        }

        /// <summary>
        /// True while a token expiry has been put off because a video is playing; the player
        /// completes it (<see cref="CompleteDeferredExpiry"/>) when the user leaves it or the
        /// video ends.
        /// </summary>
        public static bool ExpiryDeferred { get; private set; }

        /// <summary>
        /// Set by the video player while a video is actually playing (opened and settled).
        /// Only then is an expiry deferred; opening a video with a dead token, or any other
        /// page, signs out at once instead of leaving the viewer on an empty player.
        /// </summary>
        public static bool PlaybackActive { get; set; }

        /// <summary>
        /// Handle access-token expiry (401). Skips the server-side logout call (the bearer is
        /// already invalid) and drops local session state before returning to the pairing
        /// screen. Quest has no refresh-token flow, so expiry always means re-pair. Shows a
        /// popup informing the user why they were signed out — the underlying page is switched
        /// to Login first so the popup overlays a stable screen when dismissed.
        ///
        /// While the video player is open the sign-out is deferred instead: the stream URL is
        /// signed separately, so the video keeps playing, and kicking a viewer out mid-surgery
        /// is worse than losing the progress syncs that now fail.
        /// </summary>
        public static void ExpireToLogin()
        {
            if (PlaybackActive)
            {
                if (!ExpiryDeferred)
                {
                    Debug.Log("[Session] Access token expired during playback — signing out " +
                              "when the player is left.");
                    // The token is dead: never auto-resume it if the app is closed meanwhile.
                    AuthStorage.ClearCredential();
                }
                ExpiryDeferred = true;
                return;
            }
            ExpireNow();
        }

        /// <summary>
        /// Completes an expiry deferred during playback (see <see cref="ExpireToLogin"/>).
        /// Returns false, doing nothing, when none is pending — the caller then navigates as usual.
        /// </summary>
        public static bool CompleteDeferredExpiry()
        {
            if (!ExpiryDeferred) return false;
            ExpireNow();
            return true;
        }

        private static void ExpireNow()
        {
            ExpiryDeferred = false;
            Debug.Log("[Session] Access token expired — returning to login.");

            AuthStorage.ClearCredential();
            ServiceLocator.Instance.ApiClient.ClearToken();

            // The popup opens while the screen is black, together with Login, so it fades in
            // over a stable Login screen.
            ReturnToLogin(() =>
                PopupManager.Instance?.ShowDone("Your session has expired. Please sign in again."));
        }
    }
}
