using System;
using Cysharp.Threading.Tasks;
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

            // 2. Drop the bearer and all in-memory session state.
            ServiceLocator.Instance.ApiClient.ClearToken();
            AppState.Reset();          // also resets ProjectCacheRepository (V2)
            UserLocalState.Reset();
            PlaybackState.Reset();
            ThumbnailLoader.ClearCache();

            // 3. Back to the pairing screen, clearing navigation history.
            UIManager.Instance?.SwitchPage(PageType.Login);
        }
    }
}
