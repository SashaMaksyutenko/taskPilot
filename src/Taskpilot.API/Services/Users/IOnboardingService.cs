namespace Taskpilot.API.Services;

/// <summary>
/// Gives a brand-new account something to look at. Without it the first screen after
/// signing up is a dashboard of zeros, which tells a visitor nothing about the product.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Creates the starter project and its example tasks for a new user. Best-effort: a
    /// failure here must never fail the registration that triggered it.
    /// </summary>
    Task CreateStarterProjectAsync(Guid userId);
}
