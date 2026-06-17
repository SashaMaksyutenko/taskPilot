namespace Taskpilot.API.Configuration;

/// <summary>
/// Strongly-typed JWT settings, bound from the "Jwt" configuration section
/// (which is populated from the .env file: Jwt__Key, Jwt__Issuer, Jwt__Audience).
/// </summary>
public class JwtSettings
{
    /// <summary>Secret signing key (HMAC-SHA256). Kept only in .env, never in code.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Token issuer (who created the token).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (who the token is intended for).</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes. Defaults to 15 per security policy.</summary>
    public int AccessTokenMinutes { get; set; } = 15;
}
