using System;
using System.Collections.Generic;
using System.IO;
using Setsuodu.BugReport.Models;
using UnityEngine;

namespace Setsuodu.BugReport.Queue
{
    /// <summary>
    /// Simple local persistent queue. Stores one JSON file per pending report under persistentDataPath.
    /// </summary>
    public sealed class ReportQueue
    {
        private readonly string _dir;
        private readonly object _lock = new object();

        public ReportQueue(string subdirectory = "BugReportQueue")
        {
            _dir = Path.Combine(Application.persistentDataPath, subdirectory);
            if (!Directory.Exists(_dir))
                Directory.CreateDirectory(_dir);
        }

        public void Enqueue(ReportIngest report)
        {
            lock (_lock)
            {
                var file = Path.Combine(_dir, $"{report.clientReportId}.json");
                var json = JsonUtility.ToJson(report, true);
                File.WriteAllText(file, json);
            }
        }

        public List<ReportIngest> PeekAll(int max = 50)
        {
            lock (_lock)
            {
                var list = new List<ReportIngest>();
                if (!Directory.Exists(_dir)) return list;

                var files = Directory.GetFiles(_dir, "*.json");
                Array.Sort(files);
                for (var i = 0; i < files.Length && list.Count < max; i++)
                {
                    try
                    {
                        var json = File.ReadAllText(files[i]);
                        var r = JsonUtility.FromJson<ReportIngest>(json);
                        if (r != null) list.Add(r);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BugReport] Failed to read queue item {files[i]}: {e.Message}");
                    }
                }
                return list;
            }
        }

        public void Dequeue(string clientReportId)
        {
            lock (_lock)
            {
                var file = Path.Combine(_dir, $"{clientReportId}.json");
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_dir)) return 0;
                    return Directory.GetFiles(_dir, "*.json").Length;
                }
            }
        }
    }
}
