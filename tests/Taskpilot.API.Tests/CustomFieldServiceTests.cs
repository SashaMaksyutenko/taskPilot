using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for custom fields: definitions, per-task values, type validation and cascade cleanup.</summary>
public class CustomFieldServiceTests
{
    private static CustomFieldService Make(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<CustomFieldService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T" });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task CreateDefinition_ThenGetReturnsIt()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);

        var created = await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Environment", Type = "Text" });
        Assert.True(created.Succeeded);

        var defs = (await svc.GetDefinitionsAsync(owner, project)).Value!;
        var def = Assert.Single(defs);
        Assert.Equal("Environment", def.Name);
        Assert.Equal("Text", def.Type);
    }

    [Fact]
    public async Task CreateSelect_RequiresOptions_AndExposesThem()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);

        // No options → rejected.
        Assert.False((await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Sev", Type = "Select" })).Succeeded);

        var ok = await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto
        {
            Name = "Severity", Type = "Select", Options = "Low\nMedium\nHigh",
        });
        Assert.True(ok.Succeeded);
        Assert.Equal(new[] { "Low", "Medium", "High" }, ok.Value!.Options.ToArray());
    }

    [Fact]
    public async Task SetValue_Upserts_ThenClears()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);
        var fieldId = (await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Notes", Type = "Text" })).Value!.Id;

        var set = (await svc.SetTaskValueAsync(owner, task, fieldId, "hello")).Value!;
        Assert.Equal("hello", set.Single(f => f.FieldId == fieldId).Value);

        // Updating replaces the value.
        var updated = (await svc.SetTaskValueAsync(owner, task, fieldId, "world")).Value!;
        Assert.Equal("world", updated.Single(f => f.FieldId == fieldId).Value);

        // Empty clears it.
        var cleared = (await svc.SetTaskValueAsync(owner, task, fieldId, "")).Value!;
        Assert.Equal("", cleared.Single(f => f.FieldId == fieldId).Value);
    }

    [Fact]
    public async Task SetValue_ValidatesByType()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);
        var num = (await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Cost", Type = "Number" })).Value!.Id;
        var sel = (await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Sev", Type = "Select", Options = "Low\nHigh" })).Value!.Id;

        Assert.False((await svc.SetTaskValueAsync(owner, task, num, "abc")).Succeeded);
        Assert.True((await svc.SetTaskValueAsync(owner, task, num, "42.5")).Succeeded);
        Assert.False((await svc.SetTaskValueAsync(owner, task, sel, "Medium")).Succeeded); // not an option
        Assert.True((await svc.SetTaskValueAsync(owner, task, sel, "High")).Succeeded);
    }

    [Fact]
    public async Task DeleteDefinition_RemovesItsValues()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);
        var fieldId = (await svc.CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Notes", Type = "Text" })).Value!.Id;
        await svc.SetTaskValueAsync(owner, task, fieldId, "keep");

        var deleted = await svc.DeleteDefinitionAsync(owner, fieldId);
        Assert.True(deleted.Succeeded);

        Assert.Empty((await svc.GetDefinitionsAsync(owner, project)).Value!);
        Assert.Empty((await svc.GetTaskFieldsAsync(owner, task)).Value!);
        Assert.False(ctx.CustomFieldValues.Any(v => v.FieldId == fieldId));
    }

    [Fact]
    public async Task SetValue_WithoutWriteAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var fieldId = (await Make(ctx).CreateDefinitionAsync(owner, project, new CreateCustomFieldDto { Name = "Notes", Type = "Text" })).Value!.Id;

        Assert.False((await Make(ctx).SetTaskValueAsync(stranger, task, fieldId, "x")).Succeeded);
    }
}
