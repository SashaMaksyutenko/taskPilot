using System.Text.Json.Serialization;

namespace Taskpilot.API.DTOs.Integrations;

/// <summary>
/// The slice of a Telegram Bot API "Update" we care about — just incoming text messages. Telegram
/// sends far more fields; unmapped ones are ignored during deserialization.
/// </summary>
public class TelegramUpdate
{
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }
}

/// <summary>An incoming Telegram message.</summary>
public class TelegramMessage
{
    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>The chat a message came from (its id is who we reply to).</summary>
public class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
