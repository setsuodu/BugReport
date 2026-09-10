using BugReport.Server.Api.Auth;
using BugReport.Server.Api.Models;
using BugReport.Server.Api.Storage;

namespace BugReport.Server.Api.Endpoints;

public static class QueryEndpoints
{
    public static void MapQueryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/reports");

        group.MapGet("/", HandleList);
        group.MapGet("/{id:guid}", HandleGet);
        group.MapPatch("/{id:guid}/status", HandleUpdateStatus);
    }

    private static async Task<IResult> HandleList(
        string? status,
        string? projectId,
        int? page,
        int? pageSize,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 and <= 100 ? pageSize.Value : 20;

        var (items, total) = await store.ListReportsAsync(projectId, status, p, ps, http.RequestAborted);
        return Results.Ok(new ReportListResponse
        {
            Items = items,
            Page = p,
            PageSize = ps,
            Total = total
        });
    }

    private static async Task<IResult> HandleGet(
        Guid id,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        var report = await store.GetReportAsync(id, http.RequestAborted);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }

    private static async Task<IResult> HandleUpdateStatus(
        Guid id,
        StatusUpdateRequest body,
        IReportStore store,
        IConfiguration config,
        HttpContext http)
    {
        var key = config["Auth:AdminApiKey"] ?? "";
        if (!ApiKeyAuth.ValidateAdmin(http, key))
            return ApiKeyAuth.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Status) ||
            body.Status is not ("Open" or "Fixed" or "Closed"))
        {
            return Results.BadRequest(new { error = "status must be Open, Fixed, or Closed" });
        }

        var report = await store.UpdateStatusAsync(id, body.Status, http.RequestAborted);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }
}
