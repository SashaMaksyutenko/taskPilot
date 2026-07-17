using Taskpilot.API.Services;

namespace Taskpilot.API.Middleware;

/// <summary>
/// Blocks the API surface of a feature the admin has switched off in the organization
/// settings: <c>/api/marketplace</c> when the Marketplace is disabled, <c>/api/forum</c>
/// when the Forum is disabled. The client hides the navigation too, but this is the real
/// gate — a hidden link can still be typed into the address bar.
///
/// The flags live in the database and change at runtime, so they are read per matching
/// request (only for the two gated prefixes, not every request). The settings service is
/// scoped, so it is resolved from the request's service scope inside InvokeAsync.
/// </summary>
public class FeatureGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FeatureGateMiddleware> _logger;

    public FeatureGateMiddleware(RequestDelegate next, ILogger<FeatureGateMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOrganizationSettingsService settings)
    {
        var path = context.Request.Path;

        // Only touch the database when the request actually targets a gated feature.
        var gatedFeature = MatchGatedFeature(path);
        if (gatedFeature is not null)
        {
            var flags = await settings.GetFeatureFlagsAsync();
            var enabled = gatedFeature == Feature.Marketplace ? flags.MarketplaceEnabled : flags.ForumEnabled;
            if (!enabled)
            {
                _logger.LogInformation("Blocked request to disabled feature {Feature} on {Path}.", gatedFeature, path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new { error = $"The {gatedFeature} feature is currently disabled." });
                return;
            }
        }

        await _next(context);
    }

    /// <summary>The optional features this middleware can switch off.</summary>
    private enum Feature { Marketplace, Forum }

    /// <summary>Maps a request path to the feature that gates it, or null when ungated.</summary>
    private static Feature? MatchGatedFeature(PathString path)
    {
        if (path.StartsWithSegments("/api/marketplace")) return Feature.Marketplace;
        if (path.StartsWithSegments("/api/forum")) return Feature.Forum;
        return null;
    }
}
