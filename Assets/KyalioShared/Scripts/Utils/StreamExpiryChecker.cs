using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Kyalio.Utils
{
    /// <summary>
    /// Monitors StreamResponse.ExpiresAt and fires a callback one minute before expiry.
    /// Used by VideoPlayerController.
    /// </summary>
    public class StreamExpiryChecker
    {
        private CancellationTokenSource _cts;

        /// <summary>
        /// Starts monitoring; expiresAtIso must be an ISO 8601 string.
        /// </summary>
        public void Start(string expiresAtIso, Func<UniTask> onExpiringSoon)
        {
            Stop();
            _cts = new CancellationTokenSource();
            WatchAsync(expiresAtIso, onExpiringSoon, _cts.Token).Forget();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private static async UniTaskVoid WatchAsync(
            string expiresAtIso,
            Func<UniTask> onExpiringSoon,
            CancellationToken ct)
        {
            if (!DateTime.TryParse(expiresAtIso, null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var expiresAt))
            {
                Debug.LogWarning("[StreamExpiryChecker] Failed to parse expiresAt: " + expiresAtIso);
                return;
            }

            // Notify 60 seconds before expiry
            var notifyAt = expiresAt.AddSeconds(-60);
            var delay = notifyAt - DateTime.UtcNow;

            if (delay.TotalMilliseconds <= 0)
            {
                await onExpiringSoon();
                return;
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(delay.TotalMilliseconds), delayType: DelayType.Realtime, cancellationToken: ct);
                await onExpiringSoon();
            }
            catch (OperationCanceledException) { }
        }
    }
}
