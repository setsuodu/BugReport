using System.Text.Json;
using System.Text.Json.Serialization;
using BugReport.Server.Api.Models;

namespace BugReport.Server.Api.Json;

[JsonSerializable(typeof(ReportIngest))]
[JsonSerializable(typeof(IngestAccepted))]
[JsonSerializable(typeof(AttachmentInitRequest))]
[JsonSerializable(typeof(AttachmentInitResponse))]
[JsonSerializable(typeof(AttachmentCompleteRequest))]
[JsonSerializable(typeof(AttachmentCompleteResponse))]
[JsonSerializable(typeof(ReportSummary))]
[JsonSerializable(typeof(ReportDetail))]
[JsonSerializable(typeof(ReportListResponse))]
[JsonSerializable(typeof(StatusUpdateRequest))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(DeviceInfo))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<ReportSummary>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext
{
}
