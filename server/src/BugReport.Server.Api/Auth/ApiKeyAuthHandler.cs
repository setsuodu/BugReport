namespace BugReport.Server.Api.Auth;

public static class ApiKeyAuth
{
    public const string IngestHeader = "X-Api-Key";
    public const string AdminHeader = "X-Admin-Api-Key";

    public static bool ValidateIngest(HttpContext ctx, string expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey))
            return false;
        return ctx.Request.Headers.TryGetValue(IngestHeader, out var value)
               && string.Equals(value.ToString(), expectedKey, StringComparison.Ordinal);
    }

    public static bool ValidateAdmin(HttpContext ctx, string expectedKey)
    {
        if (string.IsNullOrEmpty(expectedKey))
            return false;
        return ctx.Request.Headers.TryGetValue(AdminHeader, out var value)
               && string.Equals(value.ToString(), expectedKey, StringComparison.Ordinal);
    }

    public static IResult Unauthorized() => Results.Unauthorized();
}
