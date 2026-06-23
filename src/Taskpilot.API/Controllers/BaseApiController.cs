using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Base class for API controllers. Provides shared helpers so concrete
/// controllers stay focused on their own endpoints (DRY).
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// The authenticated user's id, read from the JWT "sub" claim.
    /// Returns null when the claim is missing or not a valid Guid.
    /// </summary>
    protected Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// The caller's IP address (for audit/logging), or null when unavailable.
    /// </summary>
    protected string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// The authenticated user's email, read from the JWT "email" claim (or null).
    /// </summary>
    protected string? CurrentUserEmail() => User.FindFirstValue(JwtRegisteredClaimNames.Email);
}
