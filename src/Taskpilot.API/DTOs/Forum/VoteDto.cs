namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for voting on a reply. Value must be +1 (upvote) or -1 (downvote).</summary>
public class VoteDto
{
    public int Value { get; set; }
}

/// <summary>Result of a vote: the reply's new score and the caller's current vote.</summary>
public class VoteResultDto
{
    public Guid ReplyId { get; set; }
    public int Score { get; set; }
    public int MyVote { get; set; }
}
