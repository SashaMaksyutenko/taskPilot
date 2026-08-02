using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for CSV task import: field parsing, validation, quotes, assignee lookup and access.</summary>
public class CsvImportServiceTests
{
    private static CsvImportService Make(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<CsvImportService>.Instance);

    private static Task<List<ProjectTask>> TasksOf(TaskpilotDbContext ctx, Guid projectId) =>
        ctx.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync();

    [Fact]
    public async Task Import_CreatesTasks_WithParsedFields()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var csv = "Title,Status,Priority,Deadline\n" +
                  "Fix bug,InProgress,High,2026-08-10 00:00:00Z\n" +
                  "Write docs,Done,Low,\n";

        var result = (await Make(ctx).ImportTasksAsync(owner, project, csv)).Value!;
        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Skipped);

        var tasks = await TasksOf(ctx, project);
        var fix = tasks.Single(t => t.Title == "Fix bug");
        Assert.Equal(ProjectTaskStatus.InProgress, fix.Status);
        Assert.Equal(TaskPriority.High, fix.Priority);
        Assert.NotNull(fix.Deadline);

        var docs = tasks.Single(t => t.Title == "Write docs");
        Assert.Equal(ProjectTaskStatus.Done, docs.Status);
        Assert.NotNull(docs.CompletedAt); // Done imports get a completion time
    }

    [Fact]
    public async Task Import_SkipsRowsWithInvalidTitle()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var csv = "Title\nA\nGood title\n"; // "A" is too short

        var result = (await Make(ctx).ImportTasksAsync(owner, project, csv)).Value!;

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task Import_HandlesQuotedFieldsWithCommas()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var csv = "Title,Priority\n\"Fix, urgently\",High\n";

        var result = (await Make(ctx).ImportTasksAsync(owner, project, csv)).Value!;

        Assert.Equal(1, result.Created);
        Assert.Contains(await TasksOf(ctx, project), t => t.Title == "Fix, urgently");
    }

    [Fact]
    public async Task Import_ResolvesAssigneeByName()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = bob });
        await ctx.SaveChangesAsync();
        var csv = "Title,Assignee\nTask one,bob\nTask two,Ghost\n"; // case-insensitive; Ghost is unknown

        await Make(ctx).ImportTasksAsync(owner, project, csv);

        var tasks = await TasksOf(ctx, project);
        Assert.Equal(bob, tasks.Single(t => t.Title == "Task one").AssigneeId);
        Assert.Null(tasks.Single(t => t.Title == "Task two").AssigneeId);
    }

    [Fact]
    public async Task Import_MissingTitleColumn_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).ImportTasksAsync(owner, project, "Name,Status\nx,Backlog\n")).Succeeded);
    }

    [Fact]
    public async Task Import_WithoutWriteAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).ImportTasksAsync(stranger, project, "Title\nHello\n")).Succeeded);
    }
}
