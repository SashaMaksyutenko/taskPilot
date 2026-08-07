using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// WebAuthn / FIDO2 passkeys: register/list/remove a passkey (authenticated) and the public
/// passwordless sign-in ceremony.
/// </summary>
[ApiController]
[Authorize]
[Route("api/auth/passkeys")]
public class PasskeyController : BaseApiController
{
    private readonly IPasskeyService _passkeys;

    public PasskeyController(IPasskeyService passkeys)
    {
        _passkeys = passkeys;
    }

    /// <summary>Creation options for a new passkey (returned as WebAuthn-shaped JSON).</summary>
    [HttpPost("register/options")]
    public async Task<IActionResult> RegisterOptions()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _passkeys.GetRegisterOptionsAsync(userId.Value);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Content(result.Value!.ToJson(), "application/json");
    }

    /// <summary>Verifies the authenticator's attestation and stores the passkey.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] PasskeyRegisterCompleteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _passkeys.CompleteRegisterAsync(userId.Value, dto);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>The current user's registered passkeys.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok((await _passkeys.ListAsync(userId.Value)).Value);
    }

    /// <summary>Removes one of the current user's passkeys.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _passkeys.DeleteAsync(userId.Value, id);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Assertion options for signing in with a passkey (public).</summary>
    [HttpPost("/api/auth/passkey/login/options")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginOptions([FromBody] PasskeyLoginOptionsDto dto)
    {
        var result = await _passkeys.GetLoginOptionsAsync(dto.Email);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Completes passkey sign-in and issues auth tokens (public).</summary>
    [HttpPost("/api/auth/passkey/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PasskeyLoginCompleteDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await _passkeys.CompleteLoginAsync(dto, ip, ua);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }
}
