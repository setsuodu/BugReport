using BugReport.Server.Api.Auth;
using BugReport.Server.Api.Models;
using BugReport.Server.Api.Storage;

namespace BugReport.Server.Api.Endpoints;

public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ingest");

        group.MapPost("/reports", HandleIngestReport);
        group.MapPost("/attachments/init", HandleAttachmentInit);
        group.MapPost("/attachments/complete", HandleAttachmentComplete);
    }

    private static async Task<IResult> HandleIngestReport(
        ReportIngest body,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:IngestApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateIngest(http, key))
            return ApiKeyAuth.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.ProjectId) ||
            string.IsNullOrWhiteSpace(body.ClientReportId) ||
            string.IsNullOrWhiteSpace(body.Level) ||
            string.IsNullOrWhiteSpace(body.Message))
        {
            return Results.BadRequest(new { error = "projectId, clientReportId, level, message are required" });
        }

        var reportId = await store.InsertReportAsync(body, http.RequestAborted);
        return Results.Accepted(value: new IngestAccepted { ReportId = reportId, Accepted = true });
    }

    private static async Task<IResult> HandleAttachmentInit(
        AttachmentInitRequest body,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:IngestApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateIngest(http, key))
            return ApiKeyAuth.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.ProjectId) ||
            string.IsNullOrWhiteSpace(body.FileName) ||
            body.SizeBytes <= 0)
        {
            return Results.BadRequest(new { error = "projectId, fileName, sizeBytes are required" });
        }

        // Storage key: projectId/yyyy/MM/dd/{guid}_{fileName}
        var attachmentId = Guid.NewGuid();
        var storageKey = $"{body.ProjectId}/{DateTime.UtcNow:yyyy/MM/dd}/{attachmentId}_{SanitizeFileName(body.FileName)}";

        // Persist pending row (use generated id as attachment id by creating then returning)
        // CreateAttachmentAsync generates its own id; we need consistent id for response.
        // For simplicity we let store generate and use that.
        var id = await store.CreateAttachmentAsync(body, storageKey, http.RequestAborted);

        var endpoint = config["Storage:Endpoint"]?.TrimEnd('/') ?? "http://localhost:9000";
        var bucket = config["Storage:Bucket"] ?? "bugreport-attachments";
        // Pre-signed style placeholder: clients can PUT to this path when using path-style MinIO/S3.
        // Production should generate real pre-signed URLs via S3 SDK (kept out of AOT-critical path for v1).
        var uploadUrl = $"{endpoint}/{bucket}/{storageKey}";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        return Results.Ok(new AttachmentInitResponse
        {
            AttachmentId = id,
            UploadUrl = uploadUrl,
            ExpiresAt = expiresAt
        });
    }

    private static async Task<IResult> HandleAttachmentComplete(
        AttachmentCompleteRequest body,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:IngestApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateIngest(http, key))
            return ApiKeyAuth.Unauthorized();

        var meta = await store.GetAttachmentMetaAsync(body.AttachmentId, http.RequestAborted);
        if (meta is null)
            return Results.NotFound(new { error = "attachment not found" });

        var (projectId, storageKey, status) = meta.Value;
        if (status == "Completed")
        {
            var endpoint = config["Storage:Endpoint"]?.TrimEnd('/') ?? "http://localhost:9000";
            var bucket = config["Storage:Bucket"] ?? "bugreport-attachments";
            var url = $"{endpoint}/{bucket}/{storageKey}";
            return Results.Ok(new AttachmentCompleteResponse
            {
                AttachmentId = body.AttachmentId,
                Url = url,
                Completed = true
            });
        }

        var ep = config["Storage:Endpoint"]?.TrimEnd('/') ?? "http://localhost:9000";
        var bkt = config["Storage:Bucket"] ?? "bugreport-attachments";
        var finalUrl = $"{ep}/{bkt}/{storageKey}";

        var ok = await store.CompleteAttachmentAsync(body.AttachmentId, body.Etag, finalUrl, http.RequestAborted);
        if (!ok)
            return Results.Conflict(new { error = "attachment already completed or missing" });

        return Results.Ok(new AttachmentCompleteResponse
        {
            AttachmentId = body.AttachmentId,
            Url = finalUrl,
            Completed = true
        });
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 128 ? name[..128] : name;
    }
}
