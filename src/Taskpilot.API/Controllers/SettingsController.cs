using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Read-only organization info any signed-in user may see. Editing lives on the admin API
/// (<c>/api/admin/settings</c>); this only exposes the feature flags the client needs to
/// hide navigation for disabled features.
/// </summary>
[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : BaseApiController
{
    private readonly IOrganizationSettingsService _settings;

    public SettingsController(IOrganizationSettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>Returns which optional features (Marketplace, Forum) are enabled.</summary>
    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
    {
        var flags = await _settings.GetFeatureFlagsAsync();
        return Ok(flags);
    }

    /// <summary>
    /// Returns the organization's public branding (its name). Open to anonymous callers so
    /// the sign-in and landing pages can show the org name before a user is authenticated.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding()
    {
        var branding = await _settings.GetBrandingAsync();
        return Ok(branding);
    }
}
