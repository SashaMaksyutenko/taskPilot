namespace Taskpilot.API.Middleware;

/// <summary>
/// Enforces the read-only "Viewer" role (RBAC). An authenticated Viewer may only
/// issue safe requests (GET/HEAD/OPTIONS); any state-changing request
/// (POST/PUT/PATCH/DELETE) is rejected with 403 Forbidden.
///
/// SignalR hub paths are exempt so a Viewer can still receive real-time updates
/// and notifications — sending (which goes through REST) is still blocked.
/// </summary>
public class ViewerReadOnlyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ViewerReadOnlyMiddleware> _logger;

    public ViewerReadOnlyMiddleware(RequestDelegate next, ILogger<ViewerReadOnlyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only authenticated Viewers are restricted; everyone else passes through.
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Viewer"))
        {
            var method = context.Request.Method;
            var isSafe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

            // Real-time hubs are exempt so Viewers can still read live data.
            var isHub = context.Request.Path.StartsWithSegments("/hubs");

            if (!isSafe && !isHub)
            {
                _logger.LogWarning(
                    "Viewer write blocked. Method: {Method}, Path: {Path}", method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Viewers have read-only access." });
                return; // short-circuit: do not call the next middleware
            }
        }

        await _next(context);
    }
}
