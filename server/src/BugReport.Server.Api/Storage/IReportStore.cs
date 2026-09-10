using BugReport.Server.Api.Models;

namespace BugReport.Server.Api.Storage;

public interface IReportStore
{
    Task<Guid> InsertReportAsync(ReportIngest ingest, CancellationToken ct = default);
    Task<(List<ReportSummary> Items, int Total)> ListReportsAsync(
        string? projectId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<ReportDetail?> GetReportAsync(Guid id, CancellationToken ct = default);
    Task<ReportDetail?> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);

    Task<Guid> CreateAttachmentAsync(AttachmentInitRequest request, string storageKey, CancellationToken ct = default);
    Task<bool> CompleteAttachmentAsync(Guid attachmentId, string? etag, string url, CancellationToken ct = default);
    Task<(string ProjectId, string StorageKey, string Status)?> GetAttachmentMetaAsync(Guid attachmentId, CancellationToken ct = default);
}
