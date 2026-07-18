namespace Taskpilot.API.Common;

/// <summary>
/// Converts a Postgres connection URI into the key-value form Npgsql expects.
///
/// Managed hosts (Railway, Heroku, Render, Fly) publish the database as a single
/// <c>DATABASE_URL</c> like <c>postgresql://user:pass@host:5432/dbname</c>, while Npgsql
/// wants <c>Host=…;Port=…;Database=…;Username=…;Password=…</c>. Supporting both means the
/// app can be pointed at a managed database by copying one variable, with no hand-editing.
/// </summary>
public static class PostgresConnectionString
{
    /// <summary>
    /// Returns the Npgsql key-value connection string for <paramref name="value"/>.
    /// A value that is already in key-value form is returned unchanged; a URI is converted.
    /// Returns null when the input is blank or an unusable URI.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        // Already key-value (e.g. "Host=localhost;Port=5432;…") — nothing to convert.
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;

        // Credentials arrive percent-encoded in a URI (a password may contain '@' or '/'),
        // so they must be decoded before going into the key-value string.
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var database = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(uri.Host) || string.IsNullOrEmpty(database))
            return null;

        // Port is -1 when the URI omits it; fall back to the Postgres default.
        var port = uri.Port > 0 ? uri.Port : 5432;

        // TLS: honour an explicit ?sslmode= from the URI, else Prefer — which uses TLS when
        // the server offers it (managed hosts) but still connects to a plain local Postgres.
        // Defaulting to Require would hard-fail against any server without SSL enabled.
        var sslMode = SslModeFrom(uri.Query) ?? "Prefer";

        // Managed Postgres usually presents a self-signed certificate, so skip chain
        // validation — except when the caller explicitly asked to verify it.
        var trust = sslMode.StartsWith("Verify", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "Trust Server Certificate=true;";

        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password};" +
               $"SSL Mode={sslMode};{trust}".TrimEnd(';');
    }

    /// <summary>
    /// Reads an <c>sslmode</c> query parameter (the libpq spelling, e.g. "verify-full") and
    /// maps it to the Npgsql <c>SSL Mode</c> value. Null when absent or unrecognised.
    /// </summary>
    private static string? SslModeFrom(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(parts[1]).ToLowerInvariant() switch
            {
                "disable" => "Disable",
                "allow" => "Allow",
                "prefer" => "Prefer",
                "require" => "Require",
                "verify-ca" => "VerifyCA",
                "verify-full" => "VerifyFull",
                _ => null,
            };
        }
        return null;
    }
}
