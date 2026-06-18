namespace Taskpilot.API.Models;

/// <summary>
/// A developer's application to a marketplace task, with a cover letter and a proposed rate.
/// A user can apply to a given task only once.
/// </summary>
public class TaskApplication
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Task being applied to (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public MarketplaceTask Task { get; set; } = null!;

    /// <summary>Developer who applied (foreign key).</summary>
    public Guid ApplicantId { get; set; }

    /// <summary>Navigation to the applicant.</summary>
    public User Applicant { get; set; } = null!;

    /// <summary>Applicant's message to the poster.</summary>
    public string CoverLetter { get; set; } = string.Empty;

    /// <summary>Rate the applicant proposes for the work.</summary>
    public decimal ProposedRate { get; set; }

    /// <summary>Status of the application.</summary>
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    /// <summary>UTC time the application was submitted.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
