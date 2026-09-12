using System.Text.Json;

namespace BugReport.Server.Api.Models;

public enum ReportStatus
{
    Open,
    Fixed,
    Closed
}

public enum ReportLevel
{
    Error,
    Exception,
    Crash,
    Warning,
    Info
}

public sealed class DeviceInfo
{
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? DeviceModel { get; set; }
    public string? DeviceId { get; set; }
}

public sealed class ReportIngest
{
    public required string ProjectId { get; set; }
    public required string ClientReportId { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public string? StackTrace { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>AOT-safe: JsonElement instead of Dictionary&lt;string, object&gt;.</summary>
    public JsonElement? CustomData { get; set; }
    public List<string>? AttachmentIds { get; set; }
}

public sealed class IngestAccepted
{
    public Guid ReportId { get; set; }
    public bool Accepted { get; set; }
}

public sealed class AttachmentInitRequest
{
    public required string ProjectId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
}

public sealed class AttachmentInitResponse
{
    public Guid AttachmentId { get; set; }
    public required string UploadUrl { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AttachmentCompleteRequest
{
    public Guid AttachmentId { get; set; }
    public string? Etag { get; set; }
}

public sealed class AttachmentCompleteResponse
{
    public Guid AttachmentId { get; set; }
    public required string Url { get; set; }
    public bool Completed { get; set; }
}

public sealed class ReportSummary
{
    public Guid Id { get; set; }
    public required string ProjectId { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? AppVersion { get; set; }
}

public sealed class ReportDetail
{
    public Guid Id { get; set; }
    public required string ProjectId { get; set; }
    public required string ClientReportId { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public string? StackTrace { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string Status { get; set; }
    public JsonElement? CustomData { get; set; }
    public List<string>? AttachmentIds { get; set; }
}

public sealed class ReportListResponse
{
    public required List<ReportSummary> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class StatusUpdateRequest
{
    public required string Status { get; set; }
}

public sealed class HealthResponse
{
    public required string Status { get; set; }
}

/// <summary>ADR-0002: no anonymous error objects under AOT.</summary>
public sealed class ErrorResponse
{
    public required string Error { get; set; }
}
