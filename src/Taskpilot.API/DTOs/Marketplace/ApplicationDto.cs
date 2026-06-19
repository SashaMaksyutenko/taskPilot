namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>An application to a marketplace task as returned to clients.</summary>
public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
