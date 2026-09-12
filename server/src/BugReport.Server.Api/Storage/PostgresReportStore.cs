using System.Text.Json;
using BugReport.Server.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace BugReport.Server.Api.Storage;

public sealed class PostgresReportStore : IReportStore
{
    private readonly NpgsqlDataSource _ds;

    public PostgresReportStore(NpgsqlDataSource ds)
    {
        _ds = ds;
    }

    public async Task<Guid> InsertReportAsync(ReportIngest ingest, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO reports (
                id, project_id, client_report_id, level, message, stack_trace,
                device_platform, device_os_version, device_model, device_id,
                app_version, occurred_at, custom_data, status
            ) VALUES (
                @id, @project_id, @client_report_id, @level, @message, @stack_trace,
                @device_platform, @device_os_version, @device_model, @device_id,
                @app_version, @occurred_at, @custom_data, 'Open'
            )
            ON CONFLICT (project_id, client_report_id) DO NOTHING
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("project_id", ingest.ProjectId);
        cmd.Parameters.AddWithValue("client_report_id", ingest.ClientReportId);
        cmd.Parameters.AddWithValue("level", ingest.Level);
        cmd.Parameters.AddWithValue("message", ingest.Message);
        cmd.Parameters.AddWithValue("stack_trace", (object?)ingest.StackTrace ?? DBNull.Value);
        cmd.Parameters.AddWithValue("device_platform", (object?)ingest.DeviceInfo?.Platform ?? DBNull.Value);
        cmd.Parameters.AddWithValue("device_os_version", (object?)ingest.DeviceInfo?.OsVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("device_model", (object?)ingest.DeviceInfo?.DeviceModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("device_id", (object?)ingest.DeviceInfo?.DeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("app_version", (object?)ingest.AppVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("occurred_at", ingest.OccurredAt);

        // AOT-safe: JsonElement.GetRawText() — never Dictionary<string, object>
        if (ingest.CustomData is { ValueKind: JsonValueKind.Object or JsonValueKind.Array } je)
        {
            cmd.Parameters.Add(new NpgsqlParameter("custom_data", NpgsqlDbType.Jsonb)
            {
                Value = je.GetRawText()
            });
        }
        else
        {
            cmd.Parameters.AddWithValue("custom_data", DBNull.Value);
        }

        var result = await cmd.ExecuteScalarAsync(ct);
        Guid reportId;
        if (result is Guid existing)
        {
            reportId = existing;
        }
        else
        {
            await using var find = conn.CreateCommand();
            find.CommandText = "SELECT id FROM reports WHERE project_id = @p AND client_report_id = @c";
            find.Parameters.AddWithValue("p", ingest.ProjectId);
            find.Parameters.AddWithValue("c", ingest.ClientReportId);
            var found = await find.ExecuteScalarAsync(ct);
            reportId = found is Guid g ? g : id;
        }

        if (ingest.AttachmentIds is { Count: > 0 })
        {
            foreach (var aid in ingest.AttachmentIds)
            {
                if (!Guid.TryParse(aid, out var attachmentGuid))
                    continue;
                await using var link = conn.CreateCommand();
                link.CommandText = """
                    INSERT INTO report_attachments (report_id, attachment_id)
                    VALUES (@rid, @aid)
                    ON CONFLICT DO NOTHING
                    """;
                link.Parameters.AddWithValue("rid", reportId);
                link.Parameters.AddWithValue("aid", attachmentGuid);
                await link.ExecuteNonQueryAsync(ct);
            }
        }

        return reportId;
    }

    public async Task<(List<ReportSummary> Items, int Total)> ListReportsAsync(
        string? projectId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);

        var where = new List<string>();
        if (!string.IsNullOrEmpty(projectId)) where.Add("project_id = @project_id");
        if (!string.IsNullOrEmpty(status)) where.Add("status = @status");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM reports {whereSql}";
        if (!string.IsNullOrEmpty(projectId)) countCmd.Parameters.AddWithValue("project_id", projectId);
        if (!string.IsNullOrEmpty(status)) countCmd.Parameters.AddWithValue("status", status);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $"""
            SELECT id, project_id, level, message, status, occurred_at, created_at, app_version
            FROM reports
            {whereSql}
            ORDER BY occurred_at DESC
            LIMIT @limit OFFSET @offset
            """;
        if (!string.IsNullOrEmpty(projectId)) listCmd.Parameters.AddWithValue("project_id", projectId);
        if (!string.IsNullOrEmpty(status)) listCmd.Parameters.AddWithValue("status", status);
        listCmd.Parameters.AddWithValue("limit", pageSize);
        listCmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

        var items = new List<ReportSummary>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new ReportSummary
            {
                Id = reader.GetGuid(0),
                ProjectId = reader.GetString(1),
                Level = reader.GetString(2),
                Message = reader.GetString(3),
                Status = reader.GetString(4),
                OccurredAt = reader.GetFieldValue<DateTimeOffset>(5),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                AppVersion = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return (items, total);
    }

    public async Task<ReportDetail?> GetReportAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, client_report_id, level, message, stack_trace,
                   device_platform, device_os_version, device_model, device_id,
                   app_version, occurred_at, created_at, status, custom_data
            FROM reports WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var detail = MapDetail(reader);
        await reader.CloseAsync();

        detail.AttachmentIds = await LoadAttachmentIdsAsync(conn, id, ct);
        return detail;
    }

    public async Task<ReportDetail?> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE reports SET status = @status, updated_at = NOW()
            WHERE id = @id
            RETURNING id, project_id, client_report_id, level, message, stack_trace,
                      device_platform, device_os_version, device_model, device_id,
                      app_version, occurred_at, created_at, status, custom_data
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", status);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var detail = MapDetail(reader);
        await reader.CloseAsync();
        detail.AttachmentIds = await LoadAttachmentIdsAsync(conn, id, ct);
        return detail;
    }

    public async Task<Guid> CreateAttachmentAsync(AttachmentInitRequest request, string storageKey, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO attachments (id, project_id, file_name, content_type, size_bytes, storage_key, status)
            VALUES (@id, @project_id, @file_name, @content_type, @size_bytes, @storage_key, 'Pending')
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("project_id", request.ProjectId);
        cmd.Parameters.AddWithValue("file_name", request.FileName);
        cmd.Parameters.AddWithValue("content_type", request.ContentType);
        cmd.Parameters.AddWithValue("size_bytes", request.SizeBytes);
        cmd.Parameters.AddWithValue("storage_key", storageKey);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<bool> CompleteAttachmentAsync(Guid attachmentId, string? etag, string url, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE attachments
            SET status = 'Completed', etag = @etag, url = @url, completed_at = NOW()
            WHERE id = @id AND status = 'Pending'
            """;
        cmd.Parameters.AddWithValue("id", attachmentId);
        cmd.Parameters.AddWithValue("etag", (object?)etag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("url", url);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<(string ProjectId, string StorageKey, string Status)?> GetAttachmentMetaAsync(Guid attachmentId, CancellationToken ct = default)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT project_id, storage_key, status FROM attachments WHERE id = @id";
        cmd.Parameters.AddWithValue("id", attachmentId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static ReportDetail MapDetail(NpgsqlDataReader reader)
    {
        DeviceInfo? device = null;
        if (!reader.IsDBNull(6) || !reader.IsDBNull(7) || !reader.IsDBNull(8) || !reader.IsDBNull(9))
        {
            device = new DeviceInfo
            {
                Platform = reader.IsDBNull(6) ? null : reader.GetString(6),
                OsVersion = reader.IsDBNull(7) ? null : reader.GetString(7),
                DeviceModel = reader.IsDBNull(8) ? null : reader.GetString(8),
                DeviceId = reader.IsDBNull(9) ? null : reader.GetString(9)
            };
        }

        JsonElement? custom = null;
        if (!reader.IsDBNull(14))
        {
            var json = reader.GetString(14);
            using var doc = JsonDocument.Parse(json);
            custom = doc.RootElement.Clone();
        }

        return new ReportDetail
        {
            Id = reader.GetGuid(0),
            ProjectId = reader.GetString(1),
            ClientReportId = reader.GetString(2),
            Level = reader.GetString(3),
            Message = reader.GetString(4),
            StackTrace = reader.IsDBNull(5) ? null : reader.GetString(5),
            DeviceInfo = device,
            AppVersion = reader.IsDBNull(10) ? null : reader.GetString(10),
            OccurredAt = reader.GetFieldValue<DateTimeOffset>(11),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(12),
            Status = reader.GetString(13),
            CustomData = custom
        };
    }

    private static async Task<List<string>> LoadAttachmentIdsAsync(NpgsqlConnection conn, Guid reportId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT attachment_id FROM report_attachments WHERE report_id = @id";
        cmd.Parameters.AddWithValue("id", reportId);
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetGuid(0).ToString());
        return list;
    }
}
