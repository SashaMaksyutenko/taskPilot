namespace Taskpilot.API.DTOs.ApiKeys;

/// <summary>Request to create a new API key.</summary>
public class CreateApiKeyDto
{
    /// <summary>User-chosen label for the key.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>API key metadata (never includes the raw secret).</summary>
public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Returned once on creation — includes the full raw key (shown a single time).</summary>
public class CreatedApiKeyDto : ApiKeyDto
{
    /// <summary>The full raw key. Shown only here; store it now, it cannot be retrieved again.</summary>
    public string Key { get; set; } = string.Empty;
}
