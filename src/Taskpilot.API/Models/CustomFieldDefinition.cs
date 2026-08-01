namespace Taskpilot.API.Models;

/// <summary>The data type of a project custom field.</summary>
public enum CustomFieldType
{
    Text,
    Number,
    Select,
    Date,
}

/// <summary>
/// A user-defined field on a project's tasks (e.g. "Environment", "Story points link").
/// Every task in the project may carry a <see cref="CustomFieldValue"/> for this definition.
/// </summary>
public class CustomFieldDefinition
{
    public Guid Id { get; set; }

    /// <summary>The project this field belongs to (foreign key).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>The field's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The field's data type.</summary>
    public CustomFieldType Type { get; set; }

    /// <summary>For <see cref="CustomFieldType.Select"/>: the allowed options, one per line.</summary>
    public string? Options { get; set; }

    /// <summary>Ordering position among the project's fields.</summary>
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
