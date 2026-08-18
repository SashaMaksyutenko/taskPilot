using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;

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
    private readonly IBillingService _billing;
    private readonly IAssistantAgent _assistant;

    public SettingsController(IOrganizationSettingsService settings, IBillingService billing, IAssistantAgent assistant)
    {
        _settings = settings;
        _billing = billing;
        _assistant = assistant;
    }

    /// <summary>
    /// Returns which features are available: the org toggles (Marketplace, Forum) plus the
    /// plan-gated Pro features (AI, Automations, Whiteboard). The client uses these to hide UI.
    /// </summary>
    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
    {
        var flags = await _settings.GetFeatureFlagsAsync();
        var pro = await _billing.IsProAsync();
        flags.Ai = _assistant.IsEnabled && pro;
        flags.Automations = pro;
        flags.Whiteboard = pro;
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

    /// <summary>
    /// Streams the organization's custom logo image, or 404 when none is set. Open to
    /// anonymous callers so the sign-in and landing pages can show it before a user is
    /// authenticated (the general file endpoint requires auth).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("logo")]
    public async Task<IActionResult> GetLogo()
    {
        var result = await _settings.GetLogoAsync();
        if (!result.Succeeded)
            return NotFound();

        var file = result.Value!;
        return File(file.Content, file.ContentType);
    }
}
