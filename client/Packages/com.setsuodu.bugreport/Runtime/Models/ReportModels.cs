using System;
using System.Collections.Generic;

namespace Setsuodu.BugReport.Models
{
    [Serializable]
    public class DeviceInfo
    {
        public string platform;
        public string osVersion;
        public string deviceModel;
        public string deviceId;
    }

    [Serializable]
    public class ReportIngest
    {
        public string projectId;
        public string clientReportId;
        public string level;
        public string message;
        public string stackTrace;
        public DeviceInfo deviceInfo;
        public string appVersion;
        public string occurredAt; // ISO 8601
        public Dictionary<string, object> customData;
        public List<string> attachmentIds;
    }

    [Serializable]
    public class IngestAccepted
    {
        public string reportId;
        public bool accepted;
    }

    [Serializable]
    public class AttachmentInitRequest
    {
        public string projectId;
        public string fileName;
        public string contentType;
        public long sizeBytes;
    }

    [Serializable]
    public class AttachmentInitResponse
    {
        public string attachmentId;
        public string uploadUrl;
        public string expiresAt;
    }

    [Serializable]
    public class AttachmentCompleteRequest
    {
        public string attachmentId;
        public string etag;
    }

    [Serializable]
    public class AttachmentCompleteResponse
    {
        public string attachmentId;
        public string url;
        public bool completed;
    }
}
