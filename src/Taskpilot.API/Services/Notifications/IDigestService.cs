namespace Taskpilot.API.Services;

/// <summary>Builds and sends periodic "digest" summary emails to opted-in users.</summary>
public interface IDigestService
{
    /// <summary>
    /// Sends a digest to every user whose cadence is due (respecting their last-sent time),
    /// skipping users with nothing to report. Returns the number of digests sent.
    /// </summary>
    Task<int> SendDueDigestsAsync();
}
