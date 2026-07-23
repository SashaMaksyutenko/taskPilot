namespace Taskpilot.Integrations;

/// <summary>
/// Sends messages through the Viber Bot API. A no-op when no auth token is
/// configured, so callers never need to check first.
/// </summary>
public interface IViberSender
{
    /// <summary>True when a Viber auth token is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends a text message to a Viber user; does nothing (and never throws) when disabled.</summary>
    Task SendMessageAsync(string receiverId, string text);
}
