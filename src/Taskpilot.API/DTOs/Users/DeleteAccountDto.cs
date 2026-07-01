namespace Taskpilot.API.DTOs.Users;

/// <summary>Confirmation payload for closing (anonymizing) the current account.</summary>
public class DeleteAccountDto
{
    /// <summary>The user's current password, required to confirm.</summary>
    public string Password { get; set; } = string.Empty;
}
