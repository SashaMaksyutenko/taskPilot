namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>A review of a completed marketplace task.</summary>
public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid RaterId { get; set; }
    public string RaterName { get; set; } = string.Empty;
    public Guid RateeId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
