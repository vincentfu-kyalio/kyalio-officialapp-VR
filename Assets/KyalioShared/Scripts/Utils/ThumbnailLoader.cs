using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Core;
using Kyalio.Services;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Kyalio.Utils
{
    /// <summary>
    /// Asynchronous thumbnail loader with an LRU in-memory cache.
    ///
    /// Duplicate requests for the same URL share one download. The download is only
    /// cancelled once every caller waiting on it has cancelled, so one recycled card
    /// cannot leave the other cards with the same URL blank.
    ///
    /// When the cache is full the least recently used sprite is destroyed, unless an Image
    /// registered through <see cref="Track"/> still shows it, so eviction never blanks a
    /// card that is on screen.
    ///
    /// At most one texture is created per frame: decoding and uploading is main-thread
    /// work, and a row of cards scrolling into a virtualised list would otherwise stack
    /// several of them into one long frame.
    /// </summary>
    public static class ThumbnailLoader
    {
        private const int MaxCacheSize = 60;

        private static readonly ProfilerMarker s_createTexture = new("ThumbnailLoader.CreateTexture");
        private static readonly ProfilerMarker s_addToCache    = new("ThumbnailLoader.AddToCache");

        // Limits simultaneous HTTP downloads so 4K video streaming is not starved for bandwidth.
        private static readonly SemaphoreSlim _concurrencyLimit = new(4, 4);

        private static readonly Dictionary<string, LinkedListNode<(string url, Sprite sprite)>> _cache = new();
        private static readonly LinkedList<(string url, Sprite sprite)> _lruList = new();
        private static readonly Dictionary<string, PendingLoad> _pending = new();
        private static readonly List<Image> _trackedImages = new();

        private class PendingLoad
        {
            public readonly UniTaskCompletionSource<Sprite> Result = new();
            public readonly CancellationTokenSource Cts = new();
            public int Waiters;
        }

        /// <summary>
        /// Resolves a (possibly relative) image URL. The V2 contract returns relative
        /// auth-gated proxy paths (e.g. /api/projects/{id}/thumbnail) for project thumbnails
        /// and program logos; signed Mux playlist thumbnails come back absolute. Relative
        /// paths are joined against the API base URL; absolute URLs pass through unchanged.
        /// </summary>
        public static string Resolve(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            var baseUrl = ServiceLocator.Instance.ApiBaseUrl;
            if (string.IsNullOrEmpty(baseUrl)) return url;
            return url.StartsWith("/") ? baseUrl + url : $"{baseUrl}/{url}";
        }

        /// <summary>
        /// Registers Images that display sprites from this loader, so cache eviction never
        /// destroys a sprite one of them still shows. Call once per Image (e.g. in Awake);
        /// destroyed Images are pruned automatically. Null entries are ignored.
        /// </summary>
        public static void Track(params Image[] images)
        {
            foreach (var image in images)
                if (image != null && !_trackedImages.Contains(image))
                    _trackedImages.Add(image);
        }

        /// <summary>
        /// Loads a thumbnail and returns a Sprite; returns null on failure or cancellation.
        /// </summary>
        public static async UniTask<Sprite> LoadAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (ct.IsCancellationRequested) return null;

            if (_cache.TryGetValue(url, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.sprite;
            }

            // Join the in-flight download for this URL, or start one. A download whose
            // waiters all cancelled may still be winding down; start a fresh one instead.
            if (!_pending.TryGetValue(url, out var load) || load.Cts.IsCancellationRequested)
            {
                load = new PendingLoad();
                _pending[url] = load;
                DownloadAsync(url, load).Forget();
            }

            load.Waiters++;
            try
            {
                return await load.Result.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                // Last waiter gone before the download finished — free the slot and bandwidth.
                if (--load.Waiters == 0 && !load.Result.Task.Status.IsCompleted())
                    load.Cts.Cancel();
            }
        }

        public static void ClearCache()
        {
            foreach (var (_, sprite) in _lruList)
                DestroySprite(sprite);
            _cache.Clear();
            _lruList.Clear();
        }

        private static async UniTaskVoid DownloadAsync(string url, PendingLoad load)
        {
            var ct = load.Cts.Token;
            try
            {
                // Wait for a download slot — a cancelled load releases its place immediately.
                await _concurrencyLimit.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Finish(url, load, null, cancelled: true);
                return;
            }

            try
            {
                // nonReadable: the texture lives on the GPU only, no CPU-side copy.
                using var req = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
                var client = ServiceLocator.Instance.ApiClient;
                if (!string.IsNullOrEmpty(client.Token))
                    req.SetRequestHeader("Authorization", $"Bearer {client.Token}");
                // Auth-gated proxy thumbnails (/api/.../thumbnail) require X-App-Version too;
                // its absence returns 400 APP_VERSION_MISSING. Harmless on signed Mux URLs.
                if (!string.IsNullOrEmpty(client.AppVersion))
                    req.SetRequestHeader(ApiClient.AppVersionHeader, client.AppVersion);
                req.timeout = ApiClient.TimeoutSeconds;

                try
                {
                    await req.SendWebRequest().ToUniTask(cancellationToken: ct);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* non-2xx / network error — reported from req.result below */ }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ThumbnailLoader] Failed: {url} — {req.error}");
                    Finish(url, load, null);
                    return;
                }

                // Turning the bytes into a GPU texture is main-thread work; several downloads
                // finishing together (a row scrolling into a virtualised list) would stack it
                // into one long frame.
                await WaitForCreateSlotAsync(ct);

                Sprite sprite;
                using (s_createTexture.Auto())
                {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    sprite = Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }

                using (s_addToCache.Auto())
                    AddToCache(url, sprite);
                Finish(url, load, sprite);
            }
            catch (OperationCanceledException)
            {
                Finish(url, load, null, cancelled: true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ThumbnailLoader] Exception: {e.Message}");
                Finish(url, load, null);
            }
            finally
            {
                _concurrencyLimit.Release();
            }
        }

        // At most one texture is created per frame.
        private static int _lastCreateFrame = -1;

        private static async UniTask WaitForCreateSlotAsync(CancellationToken ct)
        {
            while (Time.frameCount == _lastCreateFrame)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            _lastCreateFrame = Time.frameCount;
        }

        private static void Finish(string url, PendingLoad load, Sprite sprite, bool cancelled = false)
        {
            // A newer load may have replaced this one after it was cancelled.
            if (_pending.TryGetValue(url, out var current) && current == load)
                _pending.Remove(url);

            if (cancelled) load.Result.TrySetCanceled();
            else load.Result.TrySetResult(sprite);
        }

        private static void AddToCache(string url, Sprite sprite)
        {
            // Evict least recently used entries that nothing shows. Sprites still on screen
            // are kept, so the cache can exceed MaxCacheSize while a long list is up.
            var candidate = _lruList.Last;
            while (_cache.Count >= MaxCacheSize && candidate != null)
            {
                var previous = candidate.Previous;
                if (!IsInUse(candidate.Value.sprite))
                {
                    _lruList.Remove(candidate);
                    _cache.Remove(candidate.Value.url);
                    DestroySprite(candidate.Value.sprite);
                }
                candidate = previous;
            }

            var newNode = _lruList.AddFirst((url, sprite));
            _cache[url] = newNode;
        }

        private static bool IsInUse(Sprite sprite)
        {
            if (sprite == null) return false;

            var inUse = false;
            for (int i = _trackedImages.Count - 1; i >= 0; i--)
            {
                var image = _trackedImages[i];
                if (image == null) { _trackedImages.RemoveAt(i); continue; }
                if (image.sprite == sprite) inUse = true;
            }
            return inUse;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
    }
}
