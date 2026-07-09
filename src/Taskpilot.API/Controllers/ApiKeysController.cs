using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.ApiKeys;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Personal API keys for the current user. All endpoints require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/apikeys")]
public class ApiKeysController : BaseApiController
{
    private readonly IApiKeyService _apiKeys;

    public ApiKeysController(IApiKeyService apiKeys)
    {
        _apiKeys = apiKeys;
    }

    /// <summary>Lists the current user's active API keys.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _apiKeys.ListAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Creates a new API key; the full key is returned once in the response.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _apiKeys.CreateAsync(userId.Value, dto.Name);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Revokes one of the current user's API keys.</summary>
    [HttpDelete("{keyId:guid}")]
    public async Task<IActionResult> Revoke(Guid keyId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _apiKeys.RevokeAsync(userId.Value, keyId);
        return result.Succeeded
            ? Ok(new { message = "API key revoked." })
            : BadRequest(new { error = result.Error });
    }
}
