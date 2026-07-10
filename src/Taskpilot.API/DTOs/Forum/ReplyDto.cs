using Taskpilot.API.DTOs.Chat;

namespace Taskpilot.API.DTOs.Forum;

/// <summary>A forum reply as returned to clients.</summary>
public class ReplyDto
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? ParentReplyId { get; set; }
    public bool IsSolution { get; set; }

    /// <summary>True if the reply was deleted; its body is blanked out.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Total score (sum of up/down votes).</summary>
    public int Score { get; set; }

    /// <summary>The current user's vote on this reply: -1, 0 or +1.</summary>
    public int MyVote { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Emoji reactions on this reply, grouped by emoji.</summary>
    public List<ReactionDto> Reactions { get; set; } = new();
}
