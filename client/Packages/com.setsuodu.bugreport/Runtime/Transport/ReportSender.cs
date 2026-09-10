using System;
using System.Collections;
using System.Text;
using Company.BugReport.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Company.BugReport.Transport
{
    public sealed class ReportSender
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public ReportSender(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public IEnumerator SendReport(ReportIngest report, Action<bool, string> onDone)
        {
            var json = JsonUtility.ToJson(report);
            // JsonUtility does not serialize Dictionary well; customData is best-effort in v1.
            using var req = new UnityWebRequest($"{_baseUrl}/api/v1/ingest/reports", "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onDone?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                onDone?.Invoke(false, $"{req.responseCode}: {req.error}");
            }
        }

        public IEnumerator InitAttachment(AttachmentInitRequest init, Action<bool, AttachmentInitResponse, string> onDone)
        {
            var json = JsonUtility.ToJson(init);
            using var req = new UnityWebRequest($"{_baseUrl}/api/v1/ingest/attachments/init", "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<AttachmentInitResponse>(req.downloadHandler.text);
                onDone?.Invoke(true, resp, null);
            }
            else
            {
                onDone?.Invoke(false, null, $"{req.responseCode}: {req.error}");
            }
        }

        public IEnumerator CompleteAttachment(AttachmentCompleteRequest complete, Action<bool, string> onDone)
        {
            var json = JsonUtility.ToJson(complete);
            using var req = new UnityWebRequest($"{_baseUrl}/api/v1/ingest/attachments/complete", "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Api-Key", _apiKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(true, req.downloadHandler.text);
            else
                onDone?.Invoke(false, $"{req.responseCode}: {req.error}");
        }

        public IEnumerator UploadBytes(string uploadUrl, byte[] data, string contentType, Action<bool, string> onDone)
        {
            using var req = UnityWebRequest.Put(uploadUrl, data);
            req.SetRequestHeader("Content-Type", contentType ?? "application/octet-stream");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(true, null);
            else
                onDone?.Invoke(false, $"{req.responseCode}: {req.error}");
        }
    }
}
