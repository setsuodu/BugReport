using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setsuodu.BugReport.Samples
{
    /// <summary>
    /// Import via Package Manager → Samples. One GameObject: BugReporter + this.
    /// Each key = one user action; capture is a single layer inside BugReporter.
    /// </summary>
    public sealed class ErrorTriggers : MonoBehaviour
    {
        [SerializeField] private KeyCode logErrorKey = KeyCode.F1;
        [SerializeField] private KeyCode throwExceptionKey = KeyCode.F2;
        [SerializeField] private KeyCode nullRefKey = KeyCode.F3;
        [SerializeField] private KeyCode manualReportKey = KeyCode.F4;
        [SerializeField] private KeyCode nestedExceptionKey = KeyCode.F5;
        [SerializeField] private KeyCode flushKey = KeyCode.F6;

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(logErrorKey)) TriggerLogError();
            if (Input.GetKeyDown(throwExceptionKey)) TriggerThrownException();
            if (Input.GetKeyDown(nullRefKey)) TriggerNullReference();
            if (Input.GetKeyDown(manualReportKey)) TriggerManualReport();
            if (Input.GetKeyDown(nestedExceptionKey)) TriggerNestedException();
            if (Input.GetKeyDown(flushKey)) TriggerFlushNow();
        }

        [ContextMenu("F1 LogError")]
        public void TriggerLogError()
        {
            Debug.LogError("[ErrorTriggers] Intentional LogError at " + DateTime.UtcNow.ToString("o"));
        }

        [ContextMenu("F2 Exception")]
        public void TriggerThrownException()
        {
            try
            {
                throw new InvalidOperationException("[ErrorTriggers] Intentional thrown InvalidOperationException");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [ContextMenu("F3 NullRef")]
        public void TriggerNullReference()
        {
            try
            {
                GameObject go = null;
                _ = go.name;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [ContextMenu("F4 Manual Report")]
        public void TriggerManualReport()
        {
            if (BugReporter.Instance == null)
            {
                Debug.LogWarning("[ErrorTriggers] No BugReporter.");
                return;
            }
            BugReporter.Instance.Report("Error", "[ErrorTriggers] Manual Report", Environment.StackTrace);
        }

        [ContextMenu("F5 Nested Exception")]
        public void TriggerNestedException()
        {
            // One LogException(outer). Stack frames still point at this method / inner throw line.
            // BugReporter keeps first callback in the frame if Unity also emits the inner.
            try
            {
                try
                {
                    throw new ArgumentOutOfRangeException("index", 99, "[ErrorTriggers] Inner argument error");
                }
                catch (Exception inner)
                {
                    throw new InvalidOperationException("[ErrorTriggers] Outer wrapper exception", inner);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [ContextMenu("F6 FlushNow")]
        public void TriggerFlushNow()
        {
            BugReporter.Instance?.FlushNow();
        }
    }
}
