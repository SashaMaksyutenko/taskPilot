using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the inbound GitHub webhook: HMAC signature verification, linking commits/PRs to
/// tasks by id, auto-closing a task when a PR that "closes" it is merged, and project scoping.
/// </summary>
public class GitHubIntegrationServiceTests
{
    private const string Secret = "testsecret";

    private static GitHubIntegrationService Make(TaskpilotDbContext ctx, ITaskService tasks) =>
        new(ctx, tasks, NullLogger<GitHubIntegrationService>.Instance);

    private static string Sign(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static async Task<Guid> ConnectedProjectAsync(TaskpilotDbContext ctx, Guid owner, GitHubMergeAction mergeAction = GitHubMergeAction.Review)
    {
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "P");
        var project = await ctx.Projects.FirstAsync(p => p.Id == projectId);
        project.GitHubRepo = "owner/name";
        project.GitHubWebhookSecret = Secret;
        project.GitHubMergeAction = mergeAction;
        await ctx.SaveChangesAsync();
        return projectId;
    }

    private static async Task<Guid> TaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T" });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Connect_SetsRepoAndSecret_ForOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var projectId = await TestDb.AddProjectAsync(ctx, user, "P");
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        var res = await svc.ConnectRepoAsync(user, projectId, "owner/name");

        Assert.True(res.Succeeded);
        Assert.Equal("owner/name", res.Value!.Repo);
        Assert.False(string.IsNullOrEmpty(res.Value.WebhookSecret));
        Assert.True((await svc.GetStatusAsync(user, projectId)).Value!.Connected);
    }

    [Fact]
    public async Task Connect_Refused_ForNonOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var other = await TestDb.AddUserAsync(ctx, "X");
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        var res = await svc.ConnectRepoAsync(other, projectId, "owner/name");
        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner);
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        var res = await svc.HandleWebhookAsync(projectId, "push", "{\"commits\":[]}", "sha256=deadbeef");

        Assert.False(res.Succeeded);
        Assert.Contains("signature", res.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Push_LinksCommit_ToReferencedTask()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner);
        var taskId = await TaskAsync(ctx, owner, projectId);
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        var body = $"{{\"commits\":[{{\"id\":\"abc123\",\"message\":\"Fix login {taskId}\",\"url\":\"https://x/commit/abc123\"}}]}}";
        var res = await svc.HandleWebhookAsync(projectId, "push", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Linked);
        Assert.Equal(1, ctx.GitHubTaskLinks.Count(l => l.TaskId == taskId && l.Kind == "Commit"));
    }

    [Fact]
    public async Task PullRequest_Merged_ClosesToDone_WhenConfigured()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner, GitHubMergeAction.Done);
        var taskId = await TaskAsync(ctx, owner, projectId);

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.ChangeStatusAsync(owner, taskId, "Done")).ReturnsAsync(Result<TaskDto>.Ok(null!));
        var svc = Make(ctx, tasks.Object);

        var body = $"{{\"action\":\"closed\",\"pull_request\":{{\"number\":7,\"title\":\"Fix\",\"body\":\"Closes {taskId}\",\"html_url\":\"https://x/pull/7\",\"merged\":true}}}}";
        var res = await svc.HandleWebhookAsync(projectId, "pull_request", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Linked);
        Assert.Equal(1, res.Value.Closed);
        tasks.Verify(t => t.ChangeStatusAsync(owner, taskId, "Done"), Times.Once);
    }

    [Fact]
    public async Task PullRequest_Merged_MovesToReview_ByDefault()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner); // default = Review
        var taskId = await TaskAsync(ctx, owner, projectId);

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.ChangeStatusAsync(owner, taskId, "Review")).ReturnsAsync(Result<TaskDto>.Ok(null!));
        var svc = Make(ctx, tasks.Object);

        var body = $"{{\"action\":\"closed\",\"pull_request\":{{\"number\":7,\"title\":\"Fix\",\"body\":\"Closes {taskId}\",\"html_url\":\"https://x/pull/7\",\"merged\":true}}}}";
        var res = await svc.HandleWebhookAsync(projectId, "pull_request", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Closed);
        // The review gate is preserved: it goes to Review, never straight to Done.
        tasks.Verify(t => t.ChangeStatusAsync(owner, taskId, "Review"), Times.Once);
        tasks.Verify(t => t.ChangeStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), "Done"), Times.Never);
    }

    [Fact]
    public async Task PullRequest_Merged_None_LinksButNeverChangesStatus()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner, GitHubMergeAction.None);
        var taskId = await TaskAsync(ctx, owner, projectId);

        var tasks = new Mock<ITaskService>();
        var svc = Make(ctx, tasks.Object);

        var body = $"{{\"action\":\"closed\",\"pull_request\":{{\"number\":7,\"title\":\"Fix\",\"body\":\"Closes {taskId}\",\"html_url\":\"https://x/pull/7\",\"merged\":true}}}}";
        var res = await svc.HandleWebhookAsync(projectId, "pull_request", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Linked);   // the PR is still linked
        Assert.Equal(0, res.Value.Closed);    // but the status is untouched
        tasks.Verify(t => t.ChangeStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetMergeAction_PersistsForOwner_RefusesOthers_AndValidates()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var other = await TestDb.AddUserAsync(ctx, "X");
        var projectId = await ConnectedProjectAsync(ctx, owner);
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        Assert.Equal("Review", (await svc.GetStatusAsync(owner, projectId)).Value!.MergeAction); // default

        Assert.True((await svc.SetMergeActionAsync(owner, projectId, "Done")).Succeeded);
        Assert.Equal("Done", (await svc.GetStatusAsync(owner, projectId)).Value!.MergeAction);

        Assert.False((await svc.SetMergeActionAsync(other, projectId, "None")).Succeeded);   // not the owner
        Assert.False((await svc.SetMergeActionAsync(owner, projectId, "Nonsense")).Succeeded); // invalid value
        Assert.Equal("Done", (await svc.GetStatusAsync(owner, projectId)).Value!.MergeAction); // unchanged
    }

    [Fact]
    public async Task PullRequest_Opened_Links_ButDoesNotClose()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner);
        var taskId = await TaskAsync(ctx, owner, projectId);

        var tasks = new Mock<ITaskService>();
        var svc = Make(ctx, tasks.Object);

        var body = $"{{\"action\":\"opened\",\"pull_request\":{{\"number\":8,\"title\":\"WIP {taskId}\",\"body\":\"\",\"html_url\":\"https://x/pull/8\",\"merged\":false}}}}";
        var res = await svc.HandleWebhookAsync(projectId, "pull_request", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Linked);
        Assert.Equal(0, res.Value.Closed);
        tasks.Verify(t => t.ChangeStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Push_IgnoresTaskFromAnotherProject()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner);
        var otherProject = await TestDb.AddProjectAsync(ctx, owner, "Other");
        var strayTask = await TaskAsync(ctx, owner, otherProject);
        var svc = Make(ctx, new Mock<ITaskService>().Object);

        var body = $"{{\"commits\":[{{\"id\":\"z1\",\"message\":\"touch {strayTask}\",\"url\":\"u\"}}]}}";
        var res = await svc.HandleWebhookAsync(projectId, "push", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(0, res.Value!.Linked);
        Assert.Empty(ctx.GitHubTaskLinks);
    }

    [Fact]
    public async Task PullRequest_Merged_ClosesTaskByShortNumber()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "O");
        var projectId = await ConnectedProjectAsync(ctx, owner, GitHubMergeAction.Done);
        var taskId = await TaskAsync(ctx, owner, projectId);
        var number = ctx.ProjectTasks.Single(t => t.Id == taskId).Number; // auto-assigned per project

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.ChangeStatusAsync(owner, taskId, "Done")).ReturnsAsync(Result<TaskDto>.Ok(null!));
        var svc = Make(ctx, tasks.Object);

        var body = $"{{\"action\":\"closed\",\"pull_request\":{{\"number\":9,\"title\":\"Fix\",\"body\":\"Closes TP-{number}\",\"html_url\":\"https://x/pull/9\",\"merged\":true}}}}";
        var res = await svc.HandleWebhookAsync(projectId, "pull_request", body, Sign(Secret, body));

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Linked);
        Assert.Equal(1, res.Value.Closed);
        tasks.Verify(t => t.ChangeStatusAsync(owner, taskId, "Done"), Times.Once);
    }
}
