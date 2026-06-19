namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>Input for applying to a marketplace task.</summary>
public class ApplyDto
{
    public Guid TaskId { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal ProposedRate { get; set; }
}
