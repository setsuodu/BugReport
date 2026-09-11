using System;
using System.Collections;
using System.Text;
using Setsuodu.BugReport.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Setsuodu.BugReport.Transport
{
    public sealed class ReportSender
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly bool _debugHttp;

        public ReportSender(string baseUrl, string apiKey, bool debugHttp = true)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _debugHttp = debugHttp;
        }

        public IEnumerator SendReport(ReportIngest report, Action<bool, string> onDone)
        {
            var url = $"{_baseUrl}/api/v1/ingest/reports";
            var json = JsonUtility.ToJson(report);
            using var req = new UnityWebRequest(url, "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] → POST {url}\n{json}");

            yield return req.SendWebRequest();

            var code = req.responseCode;
            var text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] ← {(int)code} {req.result}\n{text}");

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(true, text);
            else
                onDone?.Invoke(false, $"{code}: {req.error} | body={text}");
        }

        public IEnumerator InitAttachment(AttachmentInitRequest init, Action<bool, AttachmentInitResponse, string> onDone)
        {
            var url = $"{_baseUrl}/api/v1/ingest/attachments/init";
            var json = JsonUtility.ToJson(init);
            using var req = new UnityWebRequest(url, "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] → POST {url}\n{json}");

            yield return req.SendWebRequest();

            var text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] ← {(int)req.responseCode} {req.result}\n{text}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<AttachmentInitResponse>(text);
                onDone?.Invoke(true, resp, null);
            }
            else
                onDone?.Invoke(false, null, $"{req.responseCode}: {req.error}");
        }

        public IEnumerator CompleteAttachment(AttachmentCompleteRequest complete, Action<bool, string> onDone)
        {
            var url = $"{_baseUrl}/api/v1/ingest/attachments/complete";
            var json = JsonUtility.ToJson(complete);
            using var req = new UnityWebRequest(url, "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] → POST {url}\n{json}");

            yield return req.SendWebRequest();

            var text = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] ← {(int)req.responseCode} {req.result}\n{text}");

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(true, text);
            else
                onDone?.Invoke(false, $"{req.responseCode}: {req.error}");
        }

        public IEnumerator UploadBytes(string uploadUrl, byte[] data, string contentType, Action<bool, string> onDone)
        {
            using var req = UnityWebRequest.Put(uploadUrl, data);
            req.SetRequestHeader("Content-Type", contentType ?? "application/octet-stream");
            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] → PUT {uploadUrl} ({data?.Length ?? 0} bytes)");
            yield return req.SendWebRequest();
            if (_debugHttp)
                Debug.Log($"[BugReport][HTTP] ← PUT {(int)req.responseCode} {req.result}");
            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(true, null);
            else
                onDone?.Invoke(false, $"{req.responseCode}: {req.error}");
        }
    }
}
