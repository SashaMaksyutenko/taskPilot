namespace Taskpilot.API.DTOs.Projects;

/// <summary>A task comment as returned to clients.</summary>
public class TaskCommentDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Emoji reactions on this comment, grouped by emoji.</summary>
    public List<CommentReactionDto> Reactions { get; set; } = new();
}

/// <summary>A group of reactions with the same emoji on a comment.</summary>
public class CommentReactionDto
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>Whether the current user reacted with this emoji.</summary>
    public bool Mine { get; set; }
}

/// <summary>The updated reactions for a comment (toggle response + realtime broadcast).</summary>
public class CommentReactionsUpdateDto
{
    public Guid CommentId { get; set; }

    /// <summary>The comment's task, used to broadcast the update to the right viewers.</summary>
    public Guid TaskId { get; set; }

    public List<CommentReactionDto> Reactions { get; set; } = new();
}

/// <summary>Payload to toggle an emoji reaction on a comment.</summary>
public class ReactCommentDto
{
    public string Emoji { get; set; } = string.Empty;
}
