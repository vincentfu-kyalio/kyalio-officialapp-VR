using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;
using UnityEngine;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Batches view sessions and posts project-page-session events.
    /// Max 50 view sessions per request; duplicates are deduped server-side via id.
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

        public UniTask ReportProjectPageSessionAsync(
            ProjectPageSessionRequest request, CancellationToken ct = default)
            => _client.PostBodyAsync("/api/analytics/project-page-sessions", request, ct);

        public void Enqueue(ViewSession session)
        {
            if (session != null) _queue.Add(session);
        }

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
