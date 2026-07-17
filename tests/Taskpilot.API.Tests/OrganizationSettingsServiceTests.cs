using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="OrganizationSettingsService"/>: reading the singleton settings
/// (with usage), the validation rules on an update, and the audit entry it writes.
/// </summary>
public class OrganizationSettingsServiceTests
{
    private readonly Mock<IAuditService> _audit = new();

    private OrganizationSettingsService Create(TaskpilotDbContext ctx) =>
        new(ctx, _audit.Object, NullLogger<OrganizationSettingsService>.Instance);

    /// <summary>Seeds the singleton settings row (the migration does this in production).</summary>
    private static async Task SeedSettingsAsync(TaskpilotDbContext ctx, long maxUpload, long quota)
    {
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MaxUploadBytes = maxUpload,
            StorageQuotaBytes = quota,
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Adds a file of the given size so usage totals can be checked.</summary>
    private static async Task AddFileAsync(TaskpilotDbContext ctx, Guid uploaderId, long sizeBytes)
    {
        var id = Guid.NewGuid();
        ctx.FileAttachments.Add(new FileAttachment
        {
            Id = id,
            FileName = "f.bin",
            StoredName = $"{id:N}.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            UploaderId = uploaderId,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_ReturnsSettingsPlusActualUsage()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        await SeedSettingsAsync(ctx, 5_000_000, 20_000_000);
        await AddFileAsync(ctx, uploader, 1_200_000);
        await AddFileAsync(ctx, uploader, 800_000);
        var svc = Create(ctx);

        var dto = await svc.GetAsync();

        Assert.Equal(5_000_000, dto.MaxUploadBytes);
        Assert.Equal(20_000_000, dto.StorageQuotaBytes);
        Assert.Equal(2_000_000, dto.StorageUsedBytes);
    }

    [Fact]
    public async Task Get_OnADatabaseWithoutTheRow_FallsBackToDefaults()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        var dto = await svc.GetAsync();

        Assert.Equal(OrganizationSettings.DefaultMaxUploadBytes, dto.MaxUploadBytes);
        Assert.Equal(OrganizationSettings.DefaultStorageQuotaBytes, dto.StorageQuotaBytes);
        Assert.Equal(0, dto.StorageUsedBytes);
    }

    [Fact]
    public async Task Update_PersistsAndWritesAnAuditEntry()
    {
        await using var ctx = TestDb.CreateContext();
        await SeedSettingsAsync(ctx, OrganizationSettings.DefaultMaxUploadBytes, OrganizationSettings.DefaultStorageQuotaBytes);
        var svc = Create(ctx);
        var adminId = Guid.NewGuid();

        var result = await svc.UpdateAsync(
            new UpdateOrganizationSettingsDto { MaxUploadBytes = 20_000_000, StorageQuotaBytes = 50_000_000 },
            adminId, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.Equal(20_000_000, saved.MaxUploadBytes);
        Assert.Equal(50_000_000, saved.StorageQuotaBytes);
        Assert.NotNull(saved.UpdatedAt);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.updated", adminId, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task Update_RejectsNonPositiveLimits()
    {
        await using var ctx = TestDb.CreateContext();
        await SeedSettingsAsync(ctx, OrganizationSettings.DefaultMaxUploadBytes, OrganizationSettings.DefaultStorageQuotaBytes);
        var svc = Create(ctx);

        var result = await svc.UpdateAsync(
            new UpdateOrganizationSettingsDto { MaxUploadBytes = 0, StorageQuotaBytes = 50_000_000 },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("Limits must be greater than zero.", result.Error);
        // The bad update must not have touched the stored row.
        Assert.Equal(OrganizationSettings.DefaultMaxUploadBytes, (await ctx.OrganizationSettings.SingleAsync()).MaxUploadBytes);
    }

    [Fact]
    public async Task Update_RejectsAPerFileLimitLargerThanTheQuota()
    {
        await using var ctx = TestDb.CreateContext();
        await SeedSettingsAsync(ctx, OrganizationSettings.DefaultMaxUploadBytes, OrganizationSettings.DefaultStorageQuotaBytes);
        var svc = Create(ctx);

        var result = await svc.UpdateAsync(
            new UpdateOrganizationSettingsDto { MaxUploadBytes = 100_000_000, StorageQuotaBytes = 50_000_000 },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("The per-file limit cannot exceed the storage quota.", result.Error);
    }

    [Fact]
    public async Task Update_PersistsTheFeatureFlags()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.UpdateAsync(
            new UpdateOrganizationSettingsDto
            {
                MaxUploadBytes = OrganizationSettings.DefaultMaxUploadBytes,
                StorageQuotaBytes = OrganizationSettings.DefaultStorageQuotaBytes,
                MarketplaceEnabled = false,
                ForumEnabled = true,
            },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.MarketplaceEnabled);
        Assert.True(result.Value!.ForumEnabled);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.False(saved.MarketplaceEnabled);
        Assert.True(saved.ForumEnabled);
    }

    [Fact]
    public async Task GetFeatureFlags_ReturnsTheStoredFlags()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MarketplaceEnabled = false,
            ForumEnabled = true,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var flags = await svc.GetFeatureFlagsAsync();

        Assert.False(flags.MarketplaceEnabled);
        Assert.True(flags.ForumEnabled);
    }

    [Fact]
    public async Task GetFeatureFlags_WithoutARow_DefaultsToBothEnabled()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        var flags = await svc.GetFeatureFlagsAsync();

        // A database predating the settings seed must not silently hide features.
        Assert.True(flags.MarketplaceEnabled);
        Assert.True(flags.ForumEnabled);
    }
}
