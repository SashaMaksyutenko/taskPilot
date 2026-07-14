namespace Taskpilot.API.Middleware;

/// <summary>
/// Adds the baseline security response headers. These are cheap, apply to every
/// response, and close the "browser trusts whatever we send" class of problems.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Never let the browser guess a content type (an uploaded .txt must not be
        // sniffed into HTML and executed).
        headers["X-Content-Type-Options"] = "nosniff";

        // This is an API — no page of ours should ever be framed.
        headers["X-Frame-Options"] = "DENY";

        // Don't leak our URLs (which contain ids) to third parties via Referer.
        headers["Referrer-Policy"] = "no-referrer";

        // Nothing we serve needs the camera, microphone or geolocation.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await _next(context);
    }
}
