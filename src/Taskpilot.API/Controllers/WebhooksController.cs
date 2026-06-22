using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Webhooks;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Manage the current user's outgoing webhooks.
/// </summary>
[ApiController]
[Authorize]
[Route("api/webhooks")]
public class WebhooksController : BaseApiController
{
    private readonly IWebhookService _webhooks;
    private readonly IValidator<CreateWebhookDto> _validator;

    public WebhooksController(IWebhookService webhooks, IValidator<CreateWebhookDto> validator)
    {
        _webhooks = webhooks;
        _validator = validator;
    }

    /// <summary>Lists the current user's webhooks.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _webhooks.GetForUserAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Registers a new webhook (the response includes the generated secret).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _webhooks.CreateAsync(userId.Value, dto);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Deletes a webhook.</summary>
    [HttpDelete("{webhookId:guid}")]
    public async Task<IActionResult> Delete(Guid webhookId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _webhooks.DeleteAsync(userId.Value, webhookId);
        return result.Succeeded
            ? Ok(new { message = "Webhook deleted." })
            : NotFound(new { error = result.Error });
    }
}
