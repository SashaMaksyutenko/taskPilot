using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Inbound GitHub integration. A project owner links a repo (we mint a per-project webhook
/// secret); GitHub then POSTs push/pull_request events which we verify by HMAC-SHA256 and use to
/// link commits/PRs to tasks (matched by the task's id in the message/title/body) and to
/// auto-close tasks when a PR that references them with a closing keyword is merged.
/// </summary>
public class GitHubIntegrationService : IGitHubIntegrationService
{
    private readonly TaskpilotDbContext _context;
    private readonly ITaskService _tasks;
    private readonly ILogger<GitHubIntegrationService> _logger;

    public GitHubIntegrationService(TaskpilotDbContext context, ITaskService tasks, ILogger<GitHubIntegrationService> logger)
    {
        _context = context;
        _tasks = tasks;
        _logger = logger;
    }

    // owner/name — the only shape GitHub webhooks and our validation accept.
    private static readonly Regex RepoRegex = new(@"^[\w.\-]+/[\w.\-]+$", RegexOptions.Compiled);

    // A task id, optionally preceded by a closing keyword (close/fix/resolve, any tense).
    private static readonly Regex RefRegex = new(
        @"(?:(?<kw>close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+)?(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A short task ref like "TP-142", optionally preceded by a closing keyword. The letter prefix
    // is cosmetic — the number is resolved within the repo's project. (Bare "#142" is intentionally
    // NOT matched, to avoid clashing with GitHub's own issue references.)
    private static readonly Regex RefNumberRegex = new(
        @"(?:(?<kw>close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+)?\b[A-Za-z]{2,10}-(?<num>\d{1,7})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<Result<GitHubConnectResultDto>> ConnectRepoAsync(Guid userId, Guid projectId, string repo)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Result<GitHubConnectResultDto>.Fail("Project not found.");
        if (project.OwnerId != userId) return Result<GitHubConnectResultDto>.Fail("Only the project owner can connect a repository.");

        repo = (repo ?? string.Empty).Trim();
        if (!RepoRegex.IsMatch(repo)) return Result<GitHubConnectResultDto>.Fail("Enter the repository as owner/name.");

        project.GitHubRepo = repo;
        // Keep the existing secret when reconnecting, so an already-configured webhook keeps working.
        project.GitHubWebhookSecret ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _context.SaveChangesAsync();

        return Result<GitHubConnectResultDto>.Ok(new GitHubConnectResultDto
        {
            Repo = repo,
            WebhookSecret = project.GitHubWebhookSecret,
            WebhookUrl = string.Empty, // filled in by the controller (it knows the public host)
        });
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectRepoAsync(Guid userId, Guid projectId)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Result.Fail("Project not found.");
        if (project.OwnerId != userId) return Result.Fail("Only the project owner can disconnect the repository.");

        project.GitHubRepo = null;
        project.GitHubWebhookSecret = null;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<GitHubStatusDto>> GetStatusAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<GitHubStatusDto>.Fail("You do not have access to this project.");

        var repo = await _context.Projects.Where(p => p.Id == projectId).Select(p => p.GitHubRepo).FirstOrDefaultAsync();
        return Result<GitHubStatusDto>.Ok(new GitHubStatusDto { Connected = !string.IsNullOrEmpty(repo), Repo = repo });
    }

    /// <inheritdoc />
    public async Task<Result<List<GitHubTaskLinkDto>>> GetTaskLinksAsync(Guid userId, Guid taskId)
    {
        var projectId = await _context.ProjectTasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync();
        if (projectId is null) return Result<List<GitHubTaskLinkDto>>.Fail("Task not found.");
        if (!await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<List<GitHubTaskLinkDto>>.Fail("You do not have access to this task.");

        var links = await _context.GitHubTaskLinks
            .Where(l => l.TaskId == taskId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new GitHubTaskLinkDto
            {
                Kind = l.Kind, ExternalId = l.ExternalId, Title = l.Title, Url = l.Url, State = l.State, CreatedAt = l.CreatedAt,
            })
            .ToListAsync();
        return Result<List<GitHubTaskLinkDto>>.Ok(links);
    }

    /// <inheritdoc />
    public async Task<Result<GitHubWebhookResultDto>> HandleWebhookAsync(Guid projectId, string eventType, string rawBody, string? signature)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null || string.IsNullOrEmpty(project.GitHubRepo) || string.IsNullOrEmpty(project.GitHubWebhookSecret))
            return Result<GitHubWebhookResultDto>.Fail("This project is not connected to a GitHub repository.");
        if (!ValidSignature(project.GitHubWebhookSecret, rawBody, signature))
            return Result<GitHubWebhookResultDto>.Fail("Invalid webhook signature.");

        var result = new GitHubWebhookResultDto();
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Result<GitHubWebhookResultDto>.Fail("Malformed webhook payload.");
        }

        if (eventType == "push")
            await HandlePushAsync(projectId, root, result);
        else if (eventType == "pull_request")
            await HandlePullRequestAsync(project, root, result);
        // Other events are accepted and ignored.

        await _context.SaveChangesAsync();
        return Result<GitHubWebhookResultDto>.Ok(result);
    }

    private async Task HandlePushAsync(Guid projectId, JsonElement root, GitHubWebhookResultDto result)
    {
        if (!root.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array) return;
        foreach (var commit in commits.EnumerateArray())
        {
            var message = GetString(commit, "message");
            var sha = GetString(commit, "id");
            var url = GetString(commit, "url");
            if (string.IsNullOrEmpty(sha)) continue;

            foreach (var (taskId, _) in await ResolveReferencesAsync(projectId, message))
            {
                if (await UpsertLinkAsync(taskId, "Commit", sha, FirstLine(message), url, "pushed")) result.Linked++;
            }
        }
    }

    private async Task HandlePullRequestAsync(Project project, JsonElement root, GitHubWebhookResultDto result)
    {
        var action = GetString(root, "action");
        if (!root.TryGetProperty("pull_request", out var pr)) return;

        var number = pr.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32().ToString() : "";
        var title = GetString(pr, "title");
        var body = GetString(pr, "body");
        var url = GetString(pr, "html_url");
        var merged = pr.TryGetProperty("merged", out var mg) && (mg.ValueKind == JsonValueKind.True || mg.ValueKind == JsonValueKind.False) && mg.GetBoolean();
        var state = merged ? "merged" : action == "closed" ? "closed" : "open";
        if (string.IsNullOrEmpty(number)) return;

        foreach (var (taskId, closing) in await ResolveReferencesAsync(project.Id, $"{title}\n{body}"))
        {
            if (await UpsertLinkAsync(taskId, "PullRequest", number, title, url, state)) result.Linked++;

            // Auto-close: a merged PR that says "closes/fixes/resolves <task>" moves it to Done,
            // acting as the project owner so all the usual status-change side effects run.
            if (merged && closing)
            {
                var moved = await _tasks.ChangeStatusAsync(project.OwnerId, taskId, "Done");
                if (moved.Succeeded) result.Closed++;
                else _logger.LogInformation("GitHub auto-close skipped for task {TaskId}: {Error}", taskId, moved.Error);
            }
        }
    }

    /// <summary>Adds a link, or refreshes an existing one. Returns true only when a new link is created.</summary>
    private async Task<bool> UpsertLinkAsync(Guid taskId, string kind, string externalId, string title, string url, string state)
    {
        var link = await _context.GitHubTaskLinks.FirstOrDefaultAsync(l => l.TaskId == taskId && l.Kind == kind && l.ExternalId == externalId);
        if (link is null)
        {
            _context.GitHubTaskLinks.Add(new GitHubTaskLink
            {
                TaskId = taskId, Kind = kind, ExternalId = externalId, Title = Truncate(title), Url = url, State = state,
            });
            return true;
        }
        link.Title = Truncate(title);
        link.Url = url;
        link.State = state;
        link.UpdatedAt = DateTime.UtcNow;
        return false;
    }

    /// <summary>
    /// Resolves the tasks (of this project) referenced in text — by GUID or by short number
    /// ("KEY-142") — flagging those that carry a closing keyword.
    /// </summary>
    private async Task<Dictionary<Guid, bool>> ResolveReferencesAsync(Guid projectId, string text)
    {
        var result = new Dictionary<Guid, bool>();

        // GUID references, validated to belong to this project.
        var guidRefs = ParseGuidReferences(text);
        if (guidRefs.Count > 0)
        {
            var ids = guidRefs.Keys.ToList();
            var valid = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && ids.Contains(t.Id))
                .Select(t => t.Id).ToListAsync();
            foreach (var id in valid) MergeRef(result, id, guidRefs[id]);
        }

        // Short-number references (KEY-142), resolved to a task in this project by its Number.
        var numberRefs = ParseNumberReferences(text);
        if (numberRefs.Count > 0)
        {
            var nums = numberRefs.Keys.ToList();
            var matches = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && nums.Contains(t.Number))
                .Select(t => new { t.Id, t.Number }).ToListAsync();
            foreach (var m in matches) MergeRef(result, m.Id, numberRefs[m.Number]);
        }

        return result;
    }

    private static void MergeRef(Dictionary<Guid, bool> map, Guid id, bool closing) =>
        map[id] = map.TryGetValue(id, out var existing) ? existing || closing : closing;

    /// <summary>Extracts task GUIDs referenced in text, flagging those with a closing keyword.</summary>
    private static Dictionary<Guid, bool> ParseGuidReferences(string text)
    {
        var refs = new Dictionary<Guid, bool>();
        if (string.IsNullOrEmpty(text)) return refs;
        foreach (Match m in RefRegex.Matches(text))
        {
            if (!Guid.TryParse(m.Groups["id"].Value, out var id)) continue;
            var closing = m.Groups["kw"].Success;
            refs[id] = refs.TryGetValue(id, out var existing) ? existing || closing : closing;
        }
        return refs;
    }

    /// <summary>Extracts short task numbers ("KEY-142") from text, flagging those with a closing keyword.</summary>
    private static Dictionary<int, bool> ParseNumberReferences(string text)
    {
        var refs = new Dictionary<int, bool>();
        if (string.IsNullOrEmpty(text)) return refs;
        foreach (Match m in RefNumberRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups["num"].Value, out var num) || num <= 0) continue;
            var closing = m.Groups["kw"].Success;
            refs[num] = refs.TryGetValue(num, out var existing) ? existing || closing : closing;
        }
        return refs;
    }

    private static bool ValidSignature(string secret, string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;
        var provided = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        // Constant-time compare to avoid a timing side channel.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string FirstLine(string text) =>
        (text ?? "").Split('\n', 2)[0].Trim();

    private static string Truncate(string text) =>
        text.Length <= 200 ? text : text[..200];
}
