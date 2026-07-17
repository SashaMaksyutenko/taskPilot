using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Middleware;

/// <summary>
/// Restricts the admin API to a configured set of networks (Security__AdminIpAllowlist).
/// This is defence in depth on top of [Authorize(Roles = "Admin")]: even a stolen admin
/// token is useless from outside the office/VPN range.
///
/// Off by default (empty config = allow everything), like the other config-gated features.
/// Must run after UseForwardedHeaders so the client IP is the real one behind a proxy.
/// </summary>
public class AdminIpAllowlistMiddleware
{
    /// <summary>Everything under the admin API lives here (/api/admin and /api/admin/reports).</summary>
    private const string AdminPath = "/api/admin";

    private readonly RequestDelegate _next;
    private readonly ILogger<AdminIpAllowlistMiddleware> _logger;
    private readonly IpAllowlist _allowlist;

    public AdminIpAllowlistMiddleware(
        RequestDelegate next,
        IOptions<SecurityOptions> options,
        ILogger<AdminIpAllowlistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        // Parsed once at startup: the list is static config, not per-request state.
        _allowlist = IpAllowlist.Parse(options.Value.AdminIpAllowlist);

        if (_allowlist.IsEnabled)
            _logger.LogInformation("Admin IP allowlist is active for {Path}.", AdminPath);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_allowlist.IsEnabled && context.Request.Path.StartsWithSegments(AdminPath))
        {
            var ip = context.Connection.RemoteIpAddress;
            if (!_allowlist.IsAllowed(ip))
            {
                _logger.LogWarning("Admin API blocked for {Ip} on {Path}.", ip, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new { error = "Admin access is not allowed from this network." });
                return;
            }
        }

        await _next(context);
    }
}
