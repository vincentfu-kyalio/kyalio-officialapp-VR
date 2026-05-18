using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Kyalio.Services
{
    /// <summary>
    /// Low-level HTTP client that centralises serialisation/deserialisation and error handling.
    /// Call SetToken() after login to attach the JWT Bearer token to all subsequent requests.
    /// </summary>
    public class ApiClient
    {
        public const string AppVersionHeader = "X-App-Version";

        /// <summary>
        /// Fires when any endpoint returns 426 APP_VERSION_UNSUPPORTED. Subscribers should
        /// surface the forced-update UI and stop normal API flow. The originating call still
        /// throws <see cref="ApiException"/>; this is a side-channel notification.
        /// </summary>
        public static event Action<ForceUpdateInfo> OnForceUpdateRequired;

        private readonly string _baseUrl;
        private readonly string _appVersion;
        private string _token;

        public ApiClient(string baseUrl, string appVersion = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _appVersion = appVersion;
        }

        public void SetToken(string token) => _token = token;
        public void ClearToken() => _token = null;
        public string Token => _token;
        public string AppVersion => _appVersion;

        public UniTask<T> GetAsync<T>(string path, CancellationToken ct = default)
            => SendAsync<T>(UnityWebRequest.kHttpVerbGET, path, null, null, ct);

        public UniTask<T> GetAsync<T>(string path,
            IReadOnlyDictionary<string, string> extraHeaders,
            CancellationToken ct = default)
            => SendAsync<T>(UnityWebRequest.kHttpVerbGET, path, null, extraHeaders, ct);

        public UniTask<T> PostAsync<T>(string path, object body,
            IReadOnlyDictionary<string, string> extraHeaders = null,
            CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(body);
            return SendAsync<T>(UnityWebRequest.kHttpVerbPOST, path, json, extraHeaders, ct);
        }

        /// <summary>POST with no request body and no response body (expects 204).</summary>
        public UniTask PostAsync(string path, CancellationToken ct = default)
            => SendNoContentAsync(UnityWebRequest.kHttpVerbPOST, path, null, null, ct);

        /// <summary>POST with a request body but no response body (expects 204).</summary>
        public UniTask PostBodyAsync(string path, object body, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(body);
            return SendNoContentAsync(UnityWebRequest.kHttpVerbPOST, path, json, null, ct);
        }

        /// <summary>PATCH with a request body, returns deserialized response.</summary>
        public UniTask<T> PatchAsync<T>(string path, object body, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(body);
            return SendAsync<T>("PATCH", path, json, null, ct);
        }

        /// <summary>DELETE with no response body (expects 204).</summary>
        public UniTask DeleteAsync(string path, CancellationToken ct = default)
            => SendNoContentAsync(UnityWebRequest.kHttpVerbDELETE, path, null, null, ct);

        /// <summary>
        /// Opens a Server-Sent Events GET connection. Calls <paramref name="onEvent"/> for each
        /// received event; returns the first non-null value it produces.
        /// Throws <see cref="ApiException"/> on HTTP error (e.g. 403, 404).
        /// </summary>
        public async UniTask<T> SseAsync<T>(
            string path,
            IReadOnlyDictionary<string, string> extraHeaders,
            Func<string, string, T> onEvent,
            CancellationToken ct = default) where T : class
        {
            var url = _baseUrl + path;
            T terminalResult = null;

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            req.SetRequestHeader("Accept", "text/event-stream");
            req.SetRequestHeader("Cache-Control", "no-cache");
            if (!string.IsNullOrEmpty(_token))
                req.SetRequestHeader("Authorization", $"Bearer {_token}");
            if (!string.IsNullOrEmpty(_appVersion))
                req.SetRequestHeader(AppVersionHeader, _appVersion);
            if (extraHeaders != null)
                foreach (var kv in extraHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);

            req.downloadHandler = new SseDownloadHandler((eventName, data) =>
            {
                if (terminalResult == null)
                    terminalResult = onEvent(eventName, data);
            });

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* fall through to check result */ }

            if (req.result != UnityWebRequest.Result.Success)
            {
                var body = req.downloadHandler?.text ?? "";
                var code = (int)req.responseCode;
                if (code >= 500)
                    Debug.LogError($"[ApiClient] SSE {url} \u2192 {code}\n{body}");
                else
                    Debug.LogWarning($"[ApiClient] SSE {url} \u2192 {code}\n{body}");
                HandleForceUpdate(code, body);
                throw new ApiException(code, body);
            }

            if (terminalResult != null)
                return terminalResult;

            throw new InvalidOperationException($"[ApiClient] SSE {url} closed without a terminal event.");
        }

        private async UniTask<T> SendAsync<T>(
            string method, string path, string jsonBody,
            IReadOnlyDictionary<string, string> extraHeaders, CancellationToken ct)
        {
            var url = _baseUrl + path;
            using var req = new UnityWebRequest(url, method);

            if (jsonBody != null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(_token))
                req.SetRequestHeader("Authorization", $"Bearer {_token}");

            if (!string.IsNullOrEmpty(_appVersion))
                req.SetRequestHeader(AppVersionHeader, _appVersion);

            if (extraHeaders != null)
                foreach (var kv in extraHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // UniTask throws UnityWebRequestException on non-2xx — fall through to check result
                if (req.result == UnityWebRequest.Result.Success)
                    throw; // unexpected error, rethrow
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                var body = req.downloadHandler?.text ?? "";
                var code = (int)req.responseCode;
                if (code >= 500)
                    Debug.LogError($"[ApiClient] {method} {url} \u2192 {code}\n{body}");
                else
                    Debug.LogWarning($"[ApiClient] {method} {url} \u2192 {code}\n{body}");
                HandleForceUpdate(code, body);
                throw new ApiException(code, body);
            }

            var responseJson = req.downloadHandler.text;
            Debug.Log($"[ApiClient] {method} {url}\n{responseJson}");

            try
            {
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ApiClient] Deserialize failed: {e.Message}\n{responseJson}");
                throw;
            }
        }

        private async UniTask SendNoContentAsync(
            string method, string path, string jsonBody,
            IReadOnlyDictionary<string, string> extraHeaders, CancellationToken ct)
        {
            var url = _baseUrl + path;
            using var req = new UnityWebRequest(url, method);

            if (jsonBody != null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(_token))
                req.SetRequestHeader("Authorization", $"Bearer {_token}");

            if (!string.IsNullOrEmpty(_appVersion))
                req.SetRequestHeader(AppVersionHeader, _appVersion);

            if (extraHeaders != null)
                foreach (var kv in extraHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                if (req.result == UnityWebRequest.Result.Success)
                    throw;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                var body = req.downloadHandler?.text ?? "";
                var code = (int)req.responseCode;
                if (code >= 500)
                    Debug.LogError($"[ApiClient] {method} {url} \u2192 {code}\n{body}");
                else
                    Debug.LogWarning($"[ApiClient] {method} {url} \u2192 {code}\n{body}");
                HandleForceUpdate(code, body);
                throw new ApiException(code, body);
            }

            Debug.Log($"[ApiClient] {method} {url} \u2192 {req.responseCode}");
        }

        private static void HandleForceUpdate(int statusCode, string body)
        {
            if (statusCode != 426) return;
            if (OnForceUpdateRequired == null) return;

            ForceUpdateInfo info = null;
            try
            {
                info = JsonConvert.DeserializeObject<ForceUpdateInfo>(body);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApiClient] 426 body parse failed: {e.Message}\n{body}");
            }
            OnForceUpdateRequired.Invoke(info);
        }
    }

    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
