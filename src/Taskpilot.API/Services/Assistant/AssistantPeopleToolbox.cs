using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Looks up another person's PUBLIC profile by name. Delegates to
/// <see cref="IUserService.GetPublicProfileAsync"/> so the assistant is limited to exactly
/// the data shown on a user's profile page — private fields (account status, and email
/// unless the user opted to show it) are never exposed. Phone is deliberately omitted from
/// the assistant's view even though the profile page shows it.
/// </summary>
public class AssistantPeopleToolbox : IAssistantToolbox
{
    private readonly TaskpilotDbContext _context;
    private readonly IUserService _users;

    public AssistantPeopleToolbox(TaskpilotDbContext context, IUserService users)
    {
        _context = context;
        _users = users;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new("get_user_profile",
            "Returns a person's PUBLIC profile by name: role, title, bio, location, member-since date, "
            + "reputation, marketplace rating and social links. Only public profile data is available.",
            new
            {
                type = "object",
                properties = new { name = new { type = "string", description = "The person's name, or part of it." } },
                required = new[] { "name" },
            }),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "get_user_profile" => GetUserProfileAsync(argumentsJson),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> GetUserProfileAsync(string argsJson)
    {
        var name = ReadString(argsJson, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Json(new { error = "Provide a person's name." });

        var q = name.Trim().ToLower();
        var id = await _context.Users
            .Where(u => u.IsActive && u.Name.ToLower().Contains(q))
            .OrderBy(u => u.Name.ToLower() == q ? 0 : 1) // prefer an exact name match
            .ThenBy(u => u.Name.Length)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();
        if (id is null)
            return Json(new { error = $"No user matches '{name}'." });

        var result = await _users.GetPublicProfileAsync(id.Value);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        var p = result.Value!;
        return Json(new
        {
            name = p.Name,
            role = p.Role,
            title = p.Title,
            bio = p.Bio,
            location = p.Location,
            memberSince = p.MemberSince,
            email = p.Email, // already null unless the user chose to show it publicly
            website = p.Website,
            linkedIn = p.LinkedIn,
            github = p.GitHub,
            reputationPoints = p.ReputationPoints,
            averageRating = p.AverageRating,
            reviewCount = p.ReviewCount,
            badges = p.Badges,
        });
    }

    // --- helpers ---

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static string? ReadString(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }
}
