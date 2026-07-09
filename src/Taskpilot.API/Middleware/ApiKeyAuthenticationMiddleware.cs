using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Taskpilot.API.Services;

namespace Taskpilot.API.Middleware;

/// <summary>
/// Lets clients authenticate with a personal API key instead of a JWT. When the
/// request has no authenticated user yet and carries a valid key (in the
/// "X-Api-Key" header or as "Authorization: Bearer tp_…"), this populates
/// HttpContext.User with the same claims a JWT login would, so [Authorize] and
/// CurrentUserId() work unchanged. Runs between UseAuthentication and UseAuthorization.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private const string RawKeyPrefix = "tp_";

    private readonly RequestDelegate _next;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeys)
    {
        // Only kick in when a JWT hasn't already authenticated the request.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            var rawKey = ExtractKey(context);
            if (!string.IsNullOrEmpty(rawKey))
            {
                var identity = await apiKeys.ResolveAsync(rawKey);
                if (identity is not null)
                {
                    var claims = new[]
                    {
                        new Claim(JwtRegisteredClaimNames.Sub, identity.UserId.ToString()),
                        new Claim(JwtRegisteredClaimNames.Email, identity.Email),
                        new Claim(ClaimTypes.Role, identity.Role),
                    };
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
                }
            }
        }

        await _next(context);
    }

    // Reads the key from the X-Api-Key header, or an "Authorization: Bearer tp_…" header.
    private static string? ExtractKey(HttpContext context)
    {
        var header = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            if (token.StartsWith(RawKeyPrefix, StringComparison.Ordinal))
                return token;
        }
        return null;
    }
}
