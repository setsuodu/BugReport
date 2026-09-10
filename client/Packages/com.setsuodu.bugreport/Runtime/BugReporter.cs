using System;
using System.Collections;
using Company.BugReport.Handlers;
using Company.BugReport.Models;
using Company.BugReport.Queue;
using Company.BugReport.Trace;
using Company.BugReport.Transport;
using UnityEngine;

namespace Company.BugReport
{
    /// <summary>
    /// Entry point. Attach to a persistent GameObject or call Init from bootstrap.
    /// </summary>
    public sealed class BugReporter : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://localhost:8080";
        [SerializeField] private string projectId = "default";
        [SerializeField] private string ingestApiKey = "";
        [SerializeField] private bool captureUnhandled = true;
        [SerializeField] private float flushIntervalSeconds = 15f;

        private ReportSender _sender;
        private ReportQueue _queue;
        private ExceptionHandlers _handlers;
        private bool _flushing;

        public static BugReporter Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sender = new ReportSender(serverBaseUrl, ingestApiKey);
            _queue = new ReportQueue();

            if (captureUnhandled)
            {
                _handlers = new ExceptionHandlers(OnLog, OnUnhandled);
            }

            StartCoroutine(FlushLoop());
        }

        private void OnDestroy()
        {
            _handlers?.Dispose();
            if (Instance == this) Instance = null;
        }

        public void Report(string level, string message, string stackTrace = null, System.Collections.Generic.Dictionary<string, object> custom = null)
        {
            var report = new ReportIngest
            {
                projectId = projectId,
                clientReportId = Guid.NewGuid().ToString("N"),
                level = level ?? "Error",
                message = message ?? "",
                stackTrace = stackTrace,
                deviceInfo = DeviceInfoCollector.Collect(),
                appVersion = Application.version,
                occurredAt = DateTime.UtcNow.ToString("o"),
                customData = custom
            };
            _queue.Enqueue(report);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            var level = type == LogType.Exception ? "Exception" : "Error";
            Report(level, condition, stackTrace);
        }

        private void OnUnhandled(Exception ex)
        {
            Report("Crash", ex.Message, ex.StackTrace);
        }

        private IEnumerator FlushLoop()
        {
            var wait = new WaitForSecondsRealtime(flushIntervalSeconds);
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

                    if (ok)
                        _queue.Dequeue(report.clientReportId);
                    else
                        break; // stop on first failure; retry later
                }
            }
            finally
            {
                _flushing = false;
            }
        }

        /// <summary>Force an immediate flush attempt (e.g. before quit).</summary>
        public void FlushNow()
        {
            if (!_flushing)
                StartCoroutine(FlushOnce());
        }
    }
}
