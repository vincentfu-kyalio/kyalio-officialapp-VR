using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Kyalio.Utils
{
    /// <summary>
    /// Asynchronous thumbnail loader with an LRU in-memory cache.
    /// Duplicate requests for the same URL are de-duplicated.
    /// Oldest sprites are evicted and their textures destroyed when the cache is full.
    /// </summary>
    public static class ThumbnailLoader
    {
        private const int MaxCacheSize = 60;

        // Limits simultaneous HTTP downloads so 4K video streaming is not starved for bandwidth.
        private static readonly SemaphoreSlim _concurrencyLimit = new(4, 4);

        private static readonly Dictionary<string, LinkedListNode<(string url, Sprite sprite)>> _cache = new();
        private static readonly LinkedList<(string url, Sprite sprite)> _lruList = new();
        private static readonly Dictionary<string, UniTaskCompletionSource<Sprite>> _pending = new();

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
        /// Loads a thumbnail and returns a Sprite; returns null on failure.
        /// </summary>
        public static async UniTask<Sprite> LoadAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url)) return null;

            if (_cache.TryGetValue(url, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.sprite;
            }

            // If the same URL is already in-flight, await the same result
            if (_pending.TryGetValue(url, out var tcs))
                return await tcs.Task;

            tcs = new UniTaskCompletionSource<Sprite>();
            _pending[url] = tcs;

            // Wait for a download slot — cancelled requests release immediately
            try
            {
                await _concurrencyLimit.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                _pending.Remove(url);
                return null;
            }

            try
            {
                using var req = UnityWebRequestTexture.GetTexture(url);
                var token = ServiceLocator.Instance.ApiClient.Token;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ThumbnailLoader] Failed: {url} — {req.error}");
                    tcs.TrySetResult(null);
                    return null;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f)
                );

                AddToCache(url, sprite);
                tcs.TrySetResult(sprite);
                return sprite;
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ThumbnailLoader] Exception: {e.Message}");
                tcs.TrySetResult(null);
                return null;
            }
            finally
            {
                _concurrencyLimit.Release();
                _pending.Remove(url);
            }
        }

        public static void ClearCache()
        {
            foreach (var node in _lruList)
            {
                if (node.sprite != null)
                    UnityEngine.Object.Destroy(node.sprite.texture);
            }
            _cache.Clear();
            _lruList.Clear();
        }

        private static void AddToCache(string url, Sprite sprite)
        {
            // Evict least recently used entry if at capacity
            if (_cache.Count >= MaxCacheSize)
            {
                var oldest = _lruList.Last;
                if (oldest != null)
                {
                    _lruList.RemoveLast();
                    _cache.Remove(oldest.Value.url);
                    if (oldest.Value.sprite != null)
                        UnityEngine.Object.Destroy(oldest.Value.sprite.texture);
                }
            }

            var newNode = _lruList.AddFirst((url, sprite));
            _cache[url] = newNode;
        }
    }
}
