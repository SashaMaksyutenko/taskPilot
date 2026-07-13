namespace Taskpilot.API.DTOs.Users;

/// <summary>One entry in the reputation history, shaped for the client.</summary>
public class ReputationEntryDto
{
    public Guid Id { get; set; }

    /// <summary>Signed points change (e.g. +15, −10).</summary>
    public int Delta { get; set; }

    /// <summary>Reason name (e.g. "TaskEarly", "WarningIssued").</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>The reputation history plus the ledger's running total.</summary>
public class ReputationHistoryDto
{
    /// <summary>Recent ledger entries, newest first.</summary>
    public List<ReputationEntryDto> Entries { get; set; } = new();

    /// <summary>Sum of every ledger entry's delta (may differ from the derived score).</summary>
    public int LedgerTotal { get; set; }
}
