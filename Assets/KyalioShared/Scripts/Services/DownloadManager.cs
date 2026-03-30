using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Core;
using Kyalio.Models;
using Kyalio.State;
using UnityEngine;
using UnityEngine.Networking;

namespace Kyalio.Services
{
    /// <summary>
    /// Video download manager: single active download with a FIFO queue.
    /// Attach to the same GameObject as AppManager (already DontDestroyOnLoad).
    /// AppManager.Awake calls Initialize(baseUrl).
    /// </summary>
    public class DownloadManager : MonoBehaviour
    {
        public static DownloadManager Instance { get; private set; }

        // ── Events ────────────────────────────────────────────────────
        /// <summary>projectId, videoId, progress(0-1)</summary>
        public event Action<string, string, float> OnProgress;
        /// <summary>projectId, videoId, filePath</summary>
        public event Action<string, string, string> OnCompleted;
        /// <summary>projectId, videoId, errorMessage</summary>
        public event Action<string, string, string> OnFailed;
        /// <summary>projectId, videoId</summary>
        public event Action<string, string> OnCancelled;
        /// <summary>Fires whenever the queue or active download state changes.</summary>
        public event Action OnQueueChanged;

        // ── State ─────────────────────────────────────────────────────
        private string _baseUrl;
        private readonly Queue<DownloadTask> _queue = new();
        private DownloadTask _current;
        private CancellationTokenSource _downloadCts;
        private bool _isProcessing;

        // ── Lifecycle ─────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        public void Initialize(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        private void OnApplicationPause(bool paused)
        {
#if UNITY_IOS
            // On iOS the system may suspend the process when backgrounded.
            // Cancel the download; the partial file is retained for Range-based resumption on restore.
            if (paused)
                _downloadCts?.Cancel();
#endif
        }

        // ── Public API ────────────────────────────────────────────────
        public void Enqueue(string projectId, string videoId, long totalBytes)
        {
            if (string.IsNullOrEmpty(_baseUrl))
            {
                Debug.LogError("[DownloadManager] _baseUrl is empty — Initialize() not called. " +
                               "Check AppManager calls downloadManager.Initialize(apiBaseUrl).");
                return;
            }
            if (IsActive(projectId, videoId))
            {
                Debug.Log($"[DownloadManager] Already active: {projectId}/{videoId}");
                return;
            }
            Debug.Log($"[DownloadManager] Enqueue: {projectId}/{videoId} ({totalBytes} bytes)");
            _queue.Enqueue(new DownloadTask(projectId, videoId, totalBytes));
            OnQueueChanged?.Invoke();
            if (!_isProcessing)
                ProcessQueueAsync().Forget();
        }

        public void Cancel(string projectId, string videoId)
        {
            if (IsDownloading(projectId, videoId))
            {
                // After cancellation the partial file is retained for Range-based resumption next time
                _downloadCts?.Cancel();
                return;
            }

            // Remove from queue
            var tmp = new List<DownloadTask>(_queue);
            _queue.Clear();
            bool removed = false;
            foreach (var t in tmp)
            {
                if (!removed && t.ProjectId == projectId && t.VideoId == videoId)
                { removed = true; continue; }
                _queue.Enqueue(t);
            }
            if (removed)
            {
                OnQueueChanged?.Invoke();
                OnCancelled?.Invoke(projectId, videoId);
            }
        }

        public bool IsDownloading(string projectId, string videoId) =>
            _current != null && _current.ProjectId == projectId && _current.VideoId == videoId;

        public bool IsQueued(string projectId, string videoId)
        {
            if (IsDownloading(projectId, videoId)) return false;
            foreach (var t in _queue)
                if (t.ProjectId == projectId && t.VideoId == videoId) return true;
            return false;
        }

        public bool IsActive(string projectId, string videoId) =>
            IsDownloading(projectId, videoId) || IsQueued(projectId, videoId);

        // ── Queue Processing ──────────────────────────────────────────
        private async UniTaskVoid ProcessQueueAsync()
        {
            _isProcessing = true;
            while (_queue.Count > 0)
            {
                _current = _queue.Dequeue();
                OnQueueChanged?.Invoke();
                await RunDownloadAsync(_current);
                _current = null;
                OnQueueChanged?.Invoke();
            }
            _isProcessing = false;
        }

        private async UniTask RunDownloadAsync(DownloadTask task)
        {
            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            var destPath = GetDownloadPath(task.ProjectId, task.VideoId);
            EnsureDownloadDir();

            long existingBytes = File.Exists(destPath) ? new FileInfo(destPath).Length : 0;
            bool useRange = existingBytes > 0;

            var url = $"{_baseUrl}/api/projects/{task.ProjectId}/videos/{task.VideoId}/download";
            UnityWebRequest req = null;

            try
            {
                req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
                var token = ServiceLocator.Instance.ApiClient.Token;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");
                if (useRange)
                    req.SetRequestHeader("Range", $"bytes={existingBytes}-");
                req.downloadHandler = new DownloadHandlerFile(destPath, useRange);

                var op = req.SendWebRequest();

                while (!op.isDone)
                {
                    ct.ThrowIfCancellationRequested();

                    long totalDownloaded = existingBytes + (long)req.downloadedBytes;
                    float progress = task.TotalBytes > 0
                        ? Mathf.Clamp01((float)totalDownloaded / task.TotalBytes)
                        : req.downloadProgress;
                    OnProgress?.Invoke(task.ProjectId, task.VideoId, progress);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // Range request sent but server returned 200 (not supported); partial file is corrupted — delete and let the user retry
                if (useRange && req.responseCode == 200)
                {
                    TryDeleteFile(destPath);
                    OnFailed?.Invoke(task.ProjectId, task.VideoId, "Server does not support resume. Please try again.");
                    return;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DownloadManager] {task.ProjectId}/{task.VideoId}: {req.error}");
                    OnFailed?.Invoke(task.ProjectId, task.VideoId, req.error);
                    return;
                }

                // Success: write to state and notify listeners
                DownloadedVideoState.Instance.AddRecord(new DownloadRecord
                {
                    ProjectId = task.ProjectId,
                    VideoId = task.VideoId,
                    FilePath = destPath,
                    DownloadedAt = DateTime.UtcNow,
                    SizeBytes = task.TotalBytes
                });
                OnProgress?.Invoke(task.ProjectId, task.VideoId, 1f);
                OnCompleted?.Invoke(task.ProjectId, task.VideoId, destPath);
            }
            catch (OperationCanceledException)
            {
                req?.Abort();
                // Partial file is retained for Range-based resumption next time
                OnCancelled?.Invoke(task.ProjectId, task.VideoId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadManager] Exception: {e.Message}");
                OnFailed?.Invoke(task.ProjectId, task.VideoId, e.Message);
            }
            finally
            {
                req?.Dispose();
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        public static string GetDownloadPath(string projectId, string videoId) =>
            Path.Combine(Application.persistentDataPath, "downloads", $"{projectId}_{videoId}.mp4");

        private static void EnsureDownloadDir()
        {
            var dir = Path.Combine(Application.persistentDataPath, "downloads");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    public class DownloadTask
    {
        public readonly string ProjectId;
        public readonly string VideoId;
        public readonly long TotalBytes;

        public DownloadTask(string projectId, string videoId, long totalBytes)
        {
            ProjectId = projectId;
            VideoId = videoId;
            TotalBytes = totalBytes;
        }
    }
}
