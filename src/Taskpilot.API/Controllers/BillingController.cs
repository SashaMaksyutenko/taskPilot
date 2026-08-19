using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.DTOs.Billing;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Workspace subscription billing: read the plan (any user), start a Pro checkout or open the
/// billing portal (admin), and receive Stripe webhooks (anonymous, signature-verified).
/// </summary>
[ApiController]
[Authorize]
[Route("api/billing")]
public class BillingController : BaseApiController
{
    private readonly IBillingService _billing;
    private readonly StripeOptions _stripeOptions;

    public BillingController(IBillingService billing, IOptions<StripeOptions> stripeOptions)
    {
        _billing = billing;
        _stripeOptions = stripeOptions.Value;
    }

    /// <summary>The workspace's current plan and limits.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status() => Ok(await _billing.GetStatusAsync());

    /// <summary>Starts a Pro subscription checkout (admin only).</summary>
    [HttpPost("checkout")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Checkout([FromBody] BillingRedirectDto dto)
    {
        var result = await _billing.CreateCheckoutAsync(CurrentUserEmail() ?? string.Empty, dto.SuccessUrl, dto.CancelUrl, dto.Annual);
        return result.Succeeded ? Ok(new BillingUrlDto { Url = result.Value! }) : BadRequest(new { error = result.Error });
    }

    /// <summary>Opens the Stripe billing portal to manage or cancel (admin only).</summary>
    [HttpPost("portal")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Portal([FromBody] BillingRedirectDto dto)
    {
        var result = await _billing.CreatePortalAsync(dto.ReturnUrl ?? dto.SuccessUrl);
        return result.Succeeded ? Ok(new BillingUrlDto { Url = result.Value! }) : BadRequest(new { error = result.Error });
    }

    /// <summary>Stripe webhook: applies subscription events to the plan. Register this URL in Stripe.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        if (!_stripeOptions.WebhookConfigured)
            return NotFound();

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (!StripeSignature.Verify(payload, signature, _stripeOptions.WebhookSecret))
            return Unauthorized();

        await _billing.ProcessWebhookAsync(payload);
        return Ok(); // 200 on a valid signature so Stripe stops retrying
    }
}

/// <summary>Client-supplied redirect URLs for a hosted Stripe flow.</summary>
public class BillingRedirectDto
{
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }

    /// <summary>Bill yearly instead of monthly (only if an annual price is configured).</summary>
    public bool Annual { get; set; }
}
