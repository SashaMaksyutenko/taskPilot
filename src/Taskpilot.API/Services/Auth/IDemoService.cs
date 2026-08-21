using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Auth;

namespace Taskpilot.API.Services;

/// <summary>
/// The no-signup demo: spins up an isolated, pre-seeded throwaway account and logs the visitor
/// straight in, and reclaims expired demo accounts in the background.
/// </summary>
public interface IDemoService
{
    /// <summary>True when the demo is turned on for this deployment.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates a fresh demo account (with sample data) and returns login tokens for it.</summary>
    Task<Result<AuthResponseDto>> CreateDemoAsync(string? ip, string? userAgent);

    /// <summary>Reclaims demo accounts older than the configured retention. Returns how many.</summary>
    Task<int> PurgeExpiredAsync();
}
