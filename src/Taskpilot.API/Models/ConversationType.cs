namespace Taskpilot.API.Models;

/// <summary>
/// Kind of a chat conversation.
/// </summary>
public enum ConversationType
{
    /// <summary>One-to-one private chat between two users.</summary>
    Direct = 0,

    /// <summary>Named group chat with many participants (e.g. per project/task later).</summary>
    Group = 1
}
