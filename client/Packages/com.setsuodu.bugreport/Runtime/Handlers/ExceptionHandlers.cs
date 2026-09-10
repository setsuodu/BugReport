using System;
using UnityEngine;

namespace Company.BugReport.Handlers
{
    /// <summary>
    /// v1: C# layer only – Application.logMessageReceived + AppDomain.UnhandledException.
    /// Lua / Java native bridges are out of scope for v1.
    /// </summary>
    public sealed class ExceptionHandlers : IDisposable
    {
        private readonly Action<string, string, LogType> _onLog;
        private readonly Action<Exception> _onUnhandled;
        private bool _disposed;

        public ExceptionHandlers(Action<string, string, LogType> onLog, Action<Exception> onUnhandled)
        {
            _onLog = onLog ?? throw new ArgumentNullException(nameof(onLog));
            _onUnhandled = onUnhandled ?? throw new ArgumentNullException(nameof(onUnhandled));

            Application.logMessageReceived += OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _onLog(condition, stackTrace, type);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                _onUnhandled(ex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.logMessageReceived -= OnLogMessageReceived;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }
    }
}
