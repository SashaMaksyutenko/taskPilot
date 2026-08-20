using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// GitHub integration: links a project to a repository and processes inbound webhooks
/// (push / pull_request) to connect commits &amp; PRs to tasks and auto-close tasks on merge.
/// No GitHub OAuth is needed — GitHub calls us; each project has its own webhook secret.
/// </summary>
public interface IGitHubIntegrationService
{
    /// <summary>Links a repository ("owner/name") to the project and returns its webhook secret (owner only).</summary>
    Task<Result<GitHubConnectResultDto>> ConnectRepoAsync(Guid userId, Guid projectId, string repo);

    /// <summary>Unlinks the repository from the project (owner only).</summary>
    Task<Result> DisconnectRepoAsync(Guid userId, Guid projectId);

    /// <summary>Reports whether the project is linked and to which repo (any member).</summary>
    Task<Result<GitHubStatusDto>> GetStatusAsync(Guid userId, Guid projectId);

    /// <summary>Sets what a merged PR does to a closed task — "None"/"Review"/"Done" (owner only).</summary>
    Task<Result> SetMergeActionAsync(Guid userId, Guid projectId, string mergeAction);

    /// <summary>Commits/PRs that reference the given task (any member of its project).</summary>
    Task<Result<List<GitHubTaskLinkDto>>> GetTaskLinksAsync(Guid userId, Guid taskId);

    /// <summary>
    /// Verifies the HMAC signature, then processes a push/pull_request delivery: links commits/PRs
    /// to referenced tasks and, when a PR is merged, moves tasks referenced with a closing keyword
    /// per the project's merge action (Review by default). <paramref name="rawBody"/> must be the
    /// exact bytes GitHub signed.
    /// </summary>
    Task<Result<GitHubWebhookResultDto>> HandleWebhookAsync(Guid projectId, string eventType, string rawBody, string? signature);
}
