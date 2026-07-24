using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests skill endorsements (spec module 10): colleagues vouch for a user's listed skills,
/// counts and the viewer's own state surface on the public profile, self/unknown-skill
/// endorsements are refused, and endorsements are pruned when a skill is dropped.
/// </summary>
public class SkillEndorsementTests
{
    private static UserService Create(TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IFileService>(), NullLogger<UserService>.Instance);

    private static async Task<Guid> AddUserWithSkillsAsync(TaskpilotDbContext ctx, string name, params string[] skills)
    {
        var id = await TestDb.AddUserAsync(ctx, name);
        var user = await ctx.Users.FirstAsync(u => u.Id == id);
        user.Skills = skills.ToList();
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Endorse_IsAToggle_AndTheCountFollows()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React", "Go");
        var endorser = await TestDb.AddUserAsync(ctx, "Endorser");
        var svc = Create(ctx);

        var first = await svc.ToggleSkillEndorsementAsync(endorser, owner, "React");
        Assert.True(first.Succeeded);
        Assert.True(first.Value!.Endorsed);
        Assert.Equal(1, first.Value.Count);
        Assert.Equal("React", first.Value.Skill);

        var second = await svc.ToggleSkillEndorsementAsync(endorser, owner, "React");
        Assert.False(second.Value!.Endorsed);
        Assert.Equal(0, second.Value.Count);
    }

    [Fact]
    public async Task Endorse_UsesTheProfilesCanonicalSpelling_CaseInsensitively()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React");
        var endorser = await TestDb.AddUserAsync(ctx, "Endorser");
        var svc = Create(ctx);

        // Endorsing "react" matches the listed "React" and stores that canonical spelling.
        var result = await svc.ToggleSkillEndorsementAsync(endorser, owner, "react");

        Assert.True(result.Succeeded);
        Assert.Equal("React", result.Value!.Skill);
        Assert.Equal("React", (await ctx.SkillEndorsements.FirstAsync()).Skill);
    }

    [Fact]
    public async Task Endorse_OwnSkill_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React");
        var svc = Create(ctx);

        var result = await svc.ToggleSkillEndorsementAsync(owner, owner, "React");

        Assert.False(result.Succeeded);
        Assert.Equal("You cannot endorse your own skills.", result.Error);
    }

    [Fact]
    public async Task Endorse_AnUnlistedSkill_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React");
        var endorser = await TestDb.AddUserAsync(ctx, "Endorser");
        var svc = Create(ctx);

        var result = await svc.ToggleSkillEndorsementAsync(endorser, owner, "Rust");

        Assert.False(result.Succeeded);
        Assert.Equal("This user does not list that skill.", result.Error);
    }

    [Fact]
    public async Task PublicProfile_ReportsCountsAndWhetherTheViewerEndorsed()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React", "Go");
        var e1 = await TestDb.AddUserAsync(ctx, "E1");
        var e2 = await TestDb.AddUserAsync(ctx, "E2");
        var svc = Create(ctx);
        await svc.ToggleSkillEndorsementAsync(e1, owner, "React");
        await svc.ToggleSkillEndorsementAsync(e2, owner, "React");

        // Seen by e1: React has 2 endorsements and e1 is one of them; Go has none.
        var asE1 = (await svc.GetPublicProfileAsync(owner, e1)).Value!;
        var reactE1 = asE1.SkillEndorsements.Single(s => s.Skill == "React");
        Assert.Equal(2, reactE1.Count);
        Assert.True(reactE1.EndorsedByViewer);
        Assert.Equal(0, asE1.SkillEndorsements.Single(s => s.Skill == "Go").Count);

        // Seen by nobody in particular: same counts, but not marked as endorsed-by-viewer.
        var anon = (await svc.GetPublicProfileAsync(owner)).Value!;
        Assert.Equal(2, anon.SkillEndorsements.Single(s => s.Skill == "React").Count);
        Assert.False(anon.SkillEndorsements.Single(s => s.Skill == "React").EndorsedByViewer);
    }

    [Fact]
    public async Task DroppingASkill_PrunesItsEndorsements_NoResurrection()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await AddUserWithSkillsAsync(ctx, "Owner", "React", "Go");
        var endorser = await TestDb.AddUserAsync(ctx, "Endorser");
        var svc = Create(ctx);
        await svc.ToggleSkillEndorsementAsync(endorser, owner, "React");

        // Owner removes React, then adds it back later.
        await svc.UpdateProfileAsync(owner, new UpdateProfileDto { Name = "Owner", Skills = ["Go"] });
        await svc.UpdateProfileAsync(owner, new UpdateProfileDto { Name = "Owner", Skills = ["Go", "React"] });

        // The old endorsement did not come back.
        var profile = (await svc.GetPublicProfileAsync(owner, endorser)).Value!;
        Assert.Equal(0, profile.SkillEndorsements.Single(s => s.Skill == "React").Count);
        Assert.Empty(await ctx.SkillEndorsements.ToListAsync());
    }
}
