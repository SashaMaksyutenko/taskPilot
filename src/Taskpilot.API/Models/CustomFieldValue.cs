namespace Taskpilot.API.Models;

/// <summary>One task's value for a <see cref="CustomFieldDefinition"/> (stored as text).</summary>
public class CustomFieldValue
{
    public Guid Id { get; set; }

    /// <summary>The task the value belongs to (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>The field definition this value is for (foreign key).</summary>
    public Guid FieldId { get; set; }

    /// <summary>Navigation to the field definition.</summary>
    public CustomFieldDefinition Field { get; set; } = null!;

    /// <summary>The value, serialized as text (numbers/dates are stored in their string form).</summary>
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
