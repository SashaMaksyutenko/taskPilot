using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// WebAuthn/FIDO2 passkeys via Fido2NetLib. The short-lived challenge from the "options" call is
/// stashed in an in-memory cache (single-instance deployment) and consumed by the follow-up
/// "complete" call.
/// </summary>
public class PasskeyService : IPasskeyService
{
    private readonly IFido2 _fido2;
    private readonly TaskpilotDbContext _context;
    private readonly IAuthService _auth;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PasskeyService> _logger;

    public PasskeyService(IFido2 fido2, TaskpilotDbContext context, IAuthService auth, IMemoryCache cache, ILogger<PasskeyService> logger)
    {
        _fido2 = fido2;
        _context = context;
        _auth = auth;
        _cache = cache;
        _logger = logger;
    }

    private static string RegKey(Guid userId) => $"passkey:reg:{userId}";
    private static string LoginKey(string ceremonyId) => $"passkey:login:{ceremonyId}";

    /// <inheritdoc />
    public async Task<Result<CredentialCreateOptions>> GetRegisterOptionsAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Result<CredentialCreateOptions>.Fail("User not found.");

        var fidoUser = new Fido2User
        {
            Id = user.Id.ToByteArray(),
            Name = user.Email,
            DisplayName = user.Name,
        };

        // Don't let the same authenticator register twice.
        var existing = await _context.UserPasskeys.Where(p => p.UserId == userId)
            .Select(p => p.CredentialId).ToListAsync();
        var exclude = existing.Select(id => new PublicKeyCredentialDescriptor(id)).ToList();

        var selection = new AuthenticatorSelection
        {
            RequireResidentKey = false,
            UserVerification = UserVerificationRequirement.Preferred,
        };

        var options = _fido2.RequestNewCredential(fidoUser, exclude, selection, AttestationConveyancePreference.None);

        _cache.Set(RegKey(userId), options.ToJson(), TimeSpan.FromMinutes(5));
        return Result<CredentialCreateOptions>.Ok(options);
    }

    /// <inheritdoc />
    public async Task<Result> CompleteRegisterAsync(Guid userId, PasskeyRegisterCompleteDto dto)
    {
        if (!_cache.TryGetValue(RegKey(userId), out string? optionsJson) || optionsJson is null)
            return Result.Fail("Registration timed out. Please try again.");
        _cache.Remove(RegKey(userId));

        var options = CredentialCreateOptions.FromJson(optionsJson);

        IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (args, ct) =>
            !await _context.UserPasskeys.AnyAsync(p => p.CredentialId == args.CredentialId, ct);

        try
        {
            var result = await _fido2.MakeNewCredentialAsync(dto.AttestationResponse, options, isUnique);
            if (result.Status != "ok" || result.Result is null)
                return Result.Fail(result.ErrorMessage ?? "Could not register the passkey.");

            var cred = result.Result;
            _context.UserPasskeys.Add(new UserPasskey
            {
                UserId = userId,
                CredentialId = cred.CredentialId,
                PublicKey = cred.PublicKey,
                UserHandle = options.User.Id,
                SignatureCounter = cred.Counter,
                CredType = cred.CredType,
                Aaguid = cred.Aaguid,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? "Passkey" : dto.Name.Trim(),
            });
            await _context.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey registration verification failed.");
            return Result.Fail("Could not verify the passkey.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<PasskeyDto>>> ListAsync(Guid userId)
    {
        var list = await _context.UserPasskeys
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PasskeyDto { Id = p.Id, Name = p.Name, CreatedAt = p.CreatedAt, LastUsedAt = p.LastUsedAt })
            .ToListAsync();
        return Result<List<PasskeyDto>>.Ok(list);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid passkeyId)
    {
        var passkey = await _context.UserPasskeys.FirstOrDefaultAsync(p => p.Id == passkeyId && p.UserId == userId);
        if (passkey is null) return Result.Fail("Passkey not found.");
        _context.UserPasskeys.Remove(passkey);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<PasskeyLoginOptionsResponseDto>> GetLoginOptionsAsync(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        var creds = user is null
            ? new List<byte[]>()
            : await _context.UserPasskeys.Where(p => p.UserId == user.Id).Select(p => p.CredentialId).ToListAsync();

        // Don't reveal whether the account exists; only that no passkey is available.
        if (user is null || creds.Count == 0)
            return Result<PasskeyLoginOptionsResponseDto>.Fail("No passkey is registered for this account.");

        var allowed = creds.Select(id => new PublicKeyCredentialDescriptor(id)).ToList();
        var options = _fido2.GetAssertionOptions(allowed, UserVerificationRequirement.Preferred);

        var ceremonyId = Guid.NewGuid().ToString("N");
        var optionsJson = options.ToJson();
        _cache.Set(LoginKey(ceremonyId), (user.Id, optionsJson), TimeSpan.FromMinutes(5));
        return Result<PasskeyLoginOptionsResponseDto>.Ok(new PasskeyLoginOptionsResponseDto { CeremonyId = ceremonyId, OptionsJson = optionsJson });
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponseDto>> CompleteLoginAsync(PasskeyLoginCompleteDto dto, string? ip, string? userAgent)
    {
        if (!_cache.TryGetValue(LoginKey(dto.CeremonyId), out (Guid userId, string optionsJson) state) || state.optionsJson is null)
            return Result<AuthResponseDto>.Fail("Sign-in timed out. Please try again.");
        _cache.Remove(LoginKey(dto.CeremonyId));

        var options = AssertionOptions.FromJson(state.optionsJson);

        var credentialId = dto.AssertionResponse.RawId;
        var passkey = await _context.UserPasskeys
            .FirstOrDefaultAsync(p => p.CredentialId == credentialId && p.UserId == state.userId);
        if (passkey is null) return Result<AuthResponseDto>.Fail("Unknown passkey.");

        IsUserHandleOwnerOfCredentialIdAsync isOwner = async (args, ct) =>
            await _context.UserPasskeys.AnyAsync(p => p.CredentialId == args.CredentialId && p.UserHandle == args.UserHandle, ct);

        try
        {
            var result = await _fido2.MakeAssertionAsync(dto.AssertionResponse, options, passkey.PublicKey, (uint)passkey.SignatureCounter, isOwner);
            if (result.Status != "ok")
                return Result<AuthResponseDto>.Fail(result.ErrorMessage ?? "Passkey verification failed.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == state.userId);
            if (user is null) return Result<AuthResponseDto>.Fail("User not found.");
            if (!user.IsActive) return Result<AuthResponseDto>.Fail("Account is disabled.");

            passkey.SignatureCounter = result.Counter;
            passkey.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var auth = await _auth.CompleteLoginAsync(user, ip, userAgent);
            return Result<AuthResponseDto>.Ok(auth);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey assertion verification failed.");
            return Result<AuthResponseDto>.Fail("Passkey verification failed.");
        }
    }
}
