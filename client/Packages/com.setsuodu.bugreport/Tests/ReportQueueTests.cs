using Setsuodu.BugReport.Models;
using Setsuodu.BugReport.Queue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Setsuodu.BugReport.Tests
{
    public class ReportQueueTests
    {
        [Test]
        public void Enqueue_And_Peek_Works()
        {
            var q = new ReportQueue("BugReportTestQueue_" + System.Guid.NewGuid().ToString("N"));
            var report = new ReportIngest
            {
                projectId = "p1",
                clientReportId = "c1",
                level = "Error",
                message = "test",
                occurredAt = System.DateTime.UtcNow.ToString("o")
            };
            q.Enqueue(report);
            Assert.AreEqual(1, q.Count);
            var items = q.PeekAll();
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("c1", items[0].clientReportId);
            q.Dequeue("c1");
            Assert.AreEqual(0, q.Count);
        }
    }
}
