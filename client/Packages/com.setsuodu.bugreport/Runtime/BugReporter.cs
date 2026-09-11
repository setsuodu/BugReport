using System;
using System.Collections;
using Setsuodu.BugReport.Handlers;
using Setsuodu.BugReport.Models;
using Setsuodu.BugReport.Queue;
using Setsuodu.BugReport.Trace;
using Setsuodu.BugReport.Transport;
using UnityEngine;

namespace Setsuodu.BugReport
{
    /// <summary>
    /// Single capture → queue → HTTP. One auto-log per frame (Unity may fire twice for nested exceptions).
    /// </summary>
    public sealed class BugReporter : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://localhost:12080";
        [SerializeField] private string projectId = "default";
        [SerializeField] private string ingestApiKey = "";
        [SerializeField] private bool captureUnhandled = true;
        [SerializeField] private float flushIntervalSeconds = 3f;
        [SerializeField] private bool debugHttp = true;
        [SerializeField] private bool clearQueueOnAwake = true;

        private ReportSender _sender;
        private ReportQueue _queue;
        private ExceptionHandlers _handlers;
        private bool _flushing;
        private int _lastLogFrame = -1;

        public static BugReporter Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BugReport] Duplicate BugReporter removed — keep one only.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sender = new ReportSender(serverBaseUrl, ingestApiKey, debugHttp);
            _queue = new ReportQueue();

            if (clearQueueOnAwake)
            {
                var n = _queue.Clear();
                if (n > 0)
                    Debug.Log($"[BugReport] Cleared {n} stale queue file(s).");
            }

            if (captureUnhandled)
                _handlers = new ExceptionHandlers(OnLog, OnUnhandled);

            Debug.Log($"[BugReport] up  url={serverBaseUrl}  project={projectId}  flush={flushIntervalSeconds}s");
            StartCoroutine(FlushLoop());
        }

        private void OnDestroy()
        {
            _handlers?.Dispose();
            if (Instance == this) Instance = null;
        }

        /// <summary>Manual report (F4 / your own code). Always enqueued.</summary>
        public void Report(string level, string message, string stackTrace = null,
            System.Collections.Generic.Dictionary<string, object> custom = null)
        {
            Enqueue(level ?? "Error", message ?? "", stackTrace, custom);
        }

        // Unity log pipeline — one per frame. Nested LogException often delivers outer then inner in the same frame;
        // we keep the first (outer), which still contains the throw site in stackTrace.
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (Time.frameCount == _lastLogFrame)
                return;
            _lastLogFrame = Time.frameCount;

            var level = type == LogType.Exception ? "Exception" : "Error";
            Enqueue(level, condition, stackTrace, null);
        }

        private void OnUnhandled(Exception ex)
        {
            Enqueue("Crash", ex.Message, ex.StackTrace, null);
        }

        private void Enqueue(string level, string message, string stackTrace,
            System.Collections.Generic.Dictionary<string, object> custom)
        {
            var report = new ReportIngest
            {
                projectId = projectId,
                clientReportId = Guid.NewGuid().ToString("N"),
                level = level,
                message = message,
                stackTrace = stackTrace,
                deviceInfo = DeviceInfoCollector.Collect(),
                appVersion = Application.version,
                occurredAt = DateTime.UtcNow.ToString("o"),
                customData = custom
            };
            _queue.Enqueue(report);
            if (debugHttp)
                Debug.Log($"[BugReport] +queue  level={level}  count={_queue.Count}  {Trim(message, 80)}");
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max) + "…";
        }

        private IEnumerator FlushLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.5f, flushIntervalSeconds));
            while (true)
            {
                yield return wait;
                if (!_flushing && Application.internetReachability != NetworkReachability.NotReachable)
                    yield return FlushOnce();
            }
        }

        private IEnumerator FlushOnce()
        {
            _flushing = true;
            try
            {
                var pending = _queue.PeekAll(20);
                foreach (var report in pending)
                {
                    var done = false;
                    var ok = false;
                    yield return _sender.SendReport(report, (success, _) =>
                    {
                        ok = success;
                        done = true;
                    });
                    while (!done) yield return null;
                    if (ok) _queue.Dequeue(report.clientReportId);
                    else break;
                }
            }
            finally
            {
                _flushing = false;
            }
        }

        public void FlushNow()
        {
            if (!_flushing)
                StartCoroutine(FlushOnce());
        }
    }
}
