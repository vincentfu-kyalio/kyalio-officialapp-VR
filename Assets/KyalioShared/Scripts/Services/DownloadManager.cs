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
    /// Attach to a DontDestroyOnLoad GameObject. Files are laid out by DownloadStorage;
    /// the legacy-storage cleanup runs from AppBootstrapper, not from here.
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

        /// <summary>
        /// Removes a completed/partial download: cancels it if active, deletes the local
        /// file and record, and notifies the server (DELETE /download, idempotent).
        /// </summary>
        public void Delete(string projectId, string videoId)
        {
            if (IsActive(projectId, videoId))
                Cancel(projectId, videoId);

            DownloadedVideoState.Instance.RemoveRecord(projectId, videoId);
            ServiceLocator.Instance?.V2?.Download?.DeleteAsync(projectId, videoId).Forget();
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

            var destPath = DownloadStorage.GetFilePath(task.ProjectId, task.VideoId);
            int headerSize = DownloadStorage.HeaderSize;

            try
            {
                DownloadStorage.EnsureRoot();
                // Start over when the file is missing, was cut off inside the header, or already
                // holds every media byte without a record (e.g. a crash before AddRecord): a
                // Range from the end would only get 416 back, failing every retry.
                long mediaBytes = File.Exists(destPath)
                    ? new FileInfo(destPath).Length - headerSize
                    : -1;
                if (mediaBytes < 0 || (task.TotalBytes > 0 && mediaBytes >= task.TotalBytes))
                    DownloadStorage.WriteHeader(destPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadManager] Prepare file failed: {e.Message}");
                OnFailed?.Invoke(task.ProjectId, task.VideoId, e.Message);
                _downloadCts?.Dispose();
                _downloadCts = null;
                return;
            }

            // Media bytes already on disk after the header; the Range offset is in media bytes.
            long existingBytes = new FileInfo(destPath).Length - headerSize;
            bool useRange = existingBytes > 0;

            var download = ServiceLocator.Instance?.V2?.Download;
            if (download == null)
            {
                OnFailed?.Invoke(task.ProjectId, task.VideoId, "Download service unavailable.");
                _downloadCts?.Dispose();
                _downloadCts = null;
                return;
            }

            UnityWebRequest req = null;
            bool canceled = false;

            try
            {
                // 1. Resolve a fresh presigned R2 URL (2-hour TTL, supports Range for resume).
                var urlResp = await download.GetDownloadUrlAsync(task.ProjectId, task.VideoId, ct);
                if (string.IsNullOrEmpty(urlResp?.DownloadUrl))
                {
                    OnFailed?.Invoke(task.ProjectId, task.VideoId, "No download URL returned.");
                    return;
                }

                // 2. Stream the bytes straight from R2. The URL is self-authenticating —
                //    do NOT attach the API Bearer or the presigned signature breaks.
                req = new UnityWebRequest(urlResp.DownloadUrl, UnityWebRequest.kHttpVerbGET);
                if (useRange)
                    req.SetRequestHeader("Range", $"bytes={existingBytes}-");
                // Always append: the file already holds the header (and any resumed bytes).
                req.downloadHandler = new DownloadHandlerFile(destPath, append: true);

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
                // 416: the partial file no longer lines up with the remote one. Either way the
                // partial file is unusable — delete it so the retry starts clean.
                if (useRange && (req.responseCode == 200 || req.responseCode == 416))
                {
                    DownloadStorage.TryDelete(destPath);
                    NotifyServerCanceled(task);
                    OnFailed?.Invoke(task.ProjectId, task.VideoId, "Server does not support resume. Please try again.");
                    return;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DownloadManager] {task.ProjectId}/{task.VideoId}: {req.error}");
                    NotifyServerCanceled(task);
                    OnFailed?.Invoke(task.ProjectId, task.VideoId, req.error);
                    return;
                }

                // 3. Success: tell the server the file is fully written, persist, notify.
                NotifyServerComplete(task);
                DownloadedVideoState.Instance.AddRecord(new DownloadRecord
                {
                    ProjectId = task.ProjectId,
                    VideoId = task.VideoId,
                    FileName = Path.GetFileName(destPath),
                    DownloadedAt = DateTime.UtcNow,
                    SizeBytes = task.TotalBytes
                });
                OnProgress?.Invoke(task.ProjectId, task.VideoId, 1f);
                OnCompleted?.Invoke(task.ProjectId, task.VideoId, destPath);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                req?.Abort();
                // Partial file is retained for Range-based resumption next time
                NotifyServerCanceled(task);
                OnCancelled?.Invoke(task.ProjectId, task.VideoId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DownloadManager] Exception: {e.Message}");
                if (!canceled) NotifyServerCanceled(task);
                OnFailed?.Invoke(task.ProjectId, task.VideoId, e.Message);
            }
            finally
            {
                req?.Dispose();
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private static void NotifyServerComplete(DownloadTask task) =>
            ServiceLocator.Instance?.V2?.Download
                ?.MarkCompleteAsync(task.ProjectId, task.VideoId).Forget();

        private static void NotifyServerCanceled(DownloadTask task) =>
            ServiceLocator.Instance?.V2?.Download
                ?.MarkCanceledAsync(task.ProjectId, task.VideoId).Forget();
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
