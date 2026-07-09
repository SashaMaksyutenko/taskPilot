using Taskpilot.API.Services;

namespace Taskpilot.API.Middleware;

/// <summary>
/// Records anonymous (not-authenticated) requests via <see cref="IVisitorService"/>
/// so admins can see how many unregistered visitors are browsing. Health probes and
/// real-time hub traffic are ignored as they are not "visitors".
/// </summary>
public class VisitorTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public VisitorTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IVisitorService visitors)
    {
        // Only count requests from visitors who are not logged in.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/health") && !path.StartsWithSegments("/hubs"))
            {
                await visitors.RecordAsync(context.Connection.RemoteIpAddress?.ToString());
            }
        }

        await _next(context);
    }
}
