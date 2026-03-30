using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using UnityEngine;

namespace Kyalio.Services
{
    /// <summary>
    /// Batches and uploads view sessions to POST /api/analytics/view-sessions.
    /// Max 50 items per request. Idempotent via session UUID.
    /// </summary>
    public class AnalyticsService
    {
        private readonly ApiClient _client;
        private readonly List<ViewSession> _queue = new();
        private const int BatchSize = 50;
        private bool _isFlushing;

        public AnalyticsService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// POST /api/analytics/project-page-sessions — fire-and-forget on page exit.
        /// </summary>
        public UniTask ReportProjectPageSessionAsync(
            ProjectPageSessionRequest request,
            CancellationToken ct = default)
            => _client.PostBodyAsync("/api/analytics/project-page-sessions", request, ct);

        public void Enqueue(ViewSession session)
        {
            if (session != null)
                _queue.Add(session);
        }

        /// <summary>
        /// Uploads all queued sessions in batches of 50.
        /// Successfully uploaded sessions are removed from the queue.
        /// </summary>
        public async UniTask FlushAsync(CancellationToken ct = default)
        {
            if (_isFlushing) return;
            _isFlushing = true;
            try
            {
                while (_queue.Count > 0 && !ct.IsCancellationRequested)
                {
                    int count = Math.Min(BatchSize, _queue.Count);
                    var batch = _queue.GetRange(0, count);
                    try
                    {
                        await _client.PostBodyAsync("/api/analytics/view-sessions", batch, ct);
                        _queue.RemoveRange(0, count);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        // Leave items in queue for next flush attempt
                        Debug.LogWarning($"[AnalyticsService] Flush failed: {e.Message}");
                        break;
                    }
                }
            }
            finally
            {
                _isFlushing = false;
            }
        }
    }
}
