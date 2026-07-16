namespace Taskpilot.API.Services;

/// <summary>
/// Records and reports anonymous (not-logged-in) visitor analytics, persisted in the
/// database so the counts survive app restarts.
/// </summary>
public interface IVisitorService
{
    /// <summary>Records one anonymous request from the given IP (hashed before storage).</summary>
    Task RecordAsync(string? ip);

    /// <summary>Number of distinct anonymous visitor IPs seen today (UTC).</summary>
    Task<int> UniqueVisitorsTodayAsync();

    /// <summary>Total anonymous requests ever recorded.</summary>
    Task<long> TotalVisitsAsync();
}
