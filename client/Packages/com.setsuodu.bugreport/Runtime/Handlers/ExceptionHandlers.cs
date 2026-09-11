using System;
using UnityEngine;

namespace Setsuodu.BugReport.Handlers
{
    /// <summary>Single subscriber for log + unhandled. C# layer only (v1).</summary>
    public sealed class ExceptionHandlers : IDisposable
    {
        private static ExceptionHandlers _live;

        private readonly Action<string, string, LogType> _onLog;
        private readonly Action<Exception> _onUnhandled;
        private bool _disposed;

        public ExceptionHandlers(Action<string, string, LogType> onLog, Action<Exception> onUnhandled)
        {
            _onLog = onLog ?? throw new ArgumentNullException(nameof(onLog));
            _onUnhandled = onUnhandled ?? throw new ArgumentNullException(nameof(onUnhandled));

            if (_live != null && !_live._disposed)
                _live.Detach();

            Application.logMessageReceived += OnLog;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            _live = this;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _onLog(condition, stackTrace, type);
        }

        private void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                _onUnhandled(ex);
        }

        private void Detach()
        {
            Application.logMessageReceived -= OnLog;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            if (_live == this) _live = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Detach();
        }
    }
}
