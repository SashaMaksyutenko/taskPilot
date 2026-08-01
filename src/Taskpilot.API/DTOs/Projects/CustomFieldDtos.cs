namespace Taskpilot.API.DTOs.Projects;

/// <summary>A project's custom-field definition as returned to clients.</summary>
public class CustomFieldDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>"Text" | "Number" | "Select" | "Date".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Allowed values for a Select field (empty for other types).</summary>
    public List<string> Options { get; set; } = new();

    public int Position { get; set; }
}

/// <summary>Payload to create a custom-field definition on a project.</summary>
public class CreateCustomFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    /// <summary>For Select fields: the options, one per line.</summary>
    public string? Options { get; set; }
}

/// <summary>One custom field together with a task's current value for it.</summary>
public class TaskFieldDto
{
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();

    /// <summary>The task's value for this field ("" when unset).</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Payload to set (or clear, with an empty string) a task's value for a field.</summary>
public class SetFieldValueDto
{
    public string Value { get; set; } = string.Empty;
}
