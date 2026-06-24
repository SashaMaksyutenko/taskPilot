namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>Input for leaving a review on a completed marketplace task.</summary>
public class RateDto
{
    /// <summary>Score from 1 to 5.</summary>
    public int Stars { get; set; }

    /// <summary>Optional written feedback.</summary>
    public string? Comment { get; set; }
}
