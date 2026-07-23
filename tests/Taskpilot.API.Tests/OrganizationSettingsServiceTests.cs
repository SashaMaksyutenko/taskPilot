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
    private readonly FakeStorage _storage = new();

    private OrganizationSettingsService Create(TaskpilotDbContext ctx) =>
        new(ctx, _audit.Object,
            new FileService(ctx, _storage, NullLogger<FileService>.Instance),
            NullLogger<OrganizationSettingsService>.Instance);

    /// <summary>An in-memory storage backend so logo tests never touch the disk.</summary>
    private sealed class FakeStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _objects = new();

        public string Name => "fake";
        public int Count => _objects.Count;

        public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _objects[storedName] = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(_objects.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
        {
            _objects.Remove(storedName);
            return Task.CompletedTask;
        }
    }

    /// <summary>Builds an uploaded image of the given content type.</summary>
    private static Microsoft.AspNetCore.Http.IFormFile ImageOf(string name = "logo.png", string contentType = "image/png")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("fake-image-bytes");
        return new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = contentType,
        };
    }

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

        var result = await svc.UpdateStorageAsync(
            new UpdateStorageDto { MaxUploadBytes = 20_000_000, StorageQuotaBytes = 50_000_000 },
            adminId, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.Equal(20_000_000, saved.MaxUploadBytes);
        Assert.Equal(50_000_000, saved.StorageQuotaBytes);
        Assert.NotNull(saved.UpdatedAt);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.storage.updated", adminId, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task UpdateStorage_LeavesTheFeatureFlagsUntouched()
    {
        // The whole-record trap: changing limits must NOT reset the flags.
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MarketplaceEnabled = false,   // start with a non-default flag
            ForumEnabled = false,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.UpdateStorageAsync(
            new UpdateStorageDto { MaxUploadBytes = 20_000_000, StorageQuotaBytes = 50_000_000 },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.True(result.Succeeded);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        // Flags survive a storage-only update.
        Assert.False(saved.MarketplaceEnabled);
        Assert.False(saved.ForumEnabled);
    }

    [Fact]
    public async Task UpdateStorage_RejectsNonPositiveLimits()
    {
        await using var ctx = TestDb.CreateContext();
        await SeedSettingsAsync(ctx, OrganizationSettings.DefaultMaxUploadBytes, OrganizationSettings.DefaultStorageQuotaBytes);
        var svc = Create(ctx);

        var result = await svc.UpdateStorageAsync(
            new UpdateStorageDto { MaxUploadBytes = 0, StorageQuotaBytes = 50_000_000 },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("Limits must be greater than zero.", result.Error);
        // The bad update must not have touched the stored row.
        Assert.Equal(OrganizationSettings.DefaultMaxUploadBytes, (await ctx.OrganizationSettings.SingleAsync()).MaxUploadBytes);
    }

    [Fact]
    public async Task UpdateStorage_RejectsAPerFileLimitLargerThanTheQuota()
    {
        await using var ctx = TestDb.CreateContext();
        await SeedSettingsAsync(ctx, OrganizationSettings.DefaultMaxUploadBytes, OrganizationSettings.DefaultStorageQuotaBytes);
        var svc = Create(ctx);

        var result = await svc.UpdateStorageAsync(
            new UpdateStorageDto { MaxUploadBytes = 100_000_000, StorageQuotaBytes = 50_000_000 },
            Guid.NewGuid(), "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("The per-file limit cannot exceed the storage quota.", result.Error);
    }

    [Fact]
    public async Task UpdateFeatures_PersistsFlagsAndLeavesLimitsUntouched()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MaxUploadBytes = 7_000_000,       // non-default limits
            StorageQuotaBytes = 42_000_000,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);
        var adminId = Guid.NewGuid();

        var result = await svc.UpdateFeaturesAsync(
            new UpdateFeaturesDto { MarketplaceEnabled = false, ForumEnabled = true },
            adminId, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.MarketplaceEnabled);
        Assert.True(result.Value!.ForumEnabled);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.False(saved.MarketplaceEnabled);
        Assert.True(saved.ForumEnabled);
        // Limits survive a features-only update.
        Assert.Equal(7_000_000, saved.MaxUploadBytes);
        Assert.Equal(42_000_000, saved.StorageQuotaBytes);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.features.updated", adminId, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task UpdateRegistration_NormalizesTheDomains_AndLeavesOtherFieldsUntouched()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MaxUploadBytes = 7_000_000,
            MarketplaceEnabled = false,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);
        var adminId = Guid.NewGuid();

        var result = await svc.UpdateRegistrationAsync(
            new UpdateRegistrationDto { AllowedEmailDomains = "@Acme.COM, , acme.com,  foo.io " },
            adminId, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        // Cleaned: lower-cased, '@' stripped, de-duplicated, blanks dropped.
        Assert.Equal("acme.com, foo.io", saved.AllowedEmailDomains);
        // Storage and feature fields survive a registration-only update.
        Assert.Equal(7_000_000, saved.MaxUploadBytes);
        Assert.False(saved.MarketplaceEnabled);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.registration.updated", adminId, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
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

    [Fact]
    public async Task UpdateGeneral_PersistsTheName_AndLeavesOtherFieldsUntouched()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MaxUploadBytes = 7_000_000,       // non-default fields that must survive
            MarketplaceEnabled = false,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);
        var adminId = Guid.NewGuid();

        var result = await svc.UpdateGeneralAsync(
            new UpdateGeneralDto { Name = "  Acme Corp  " },
            adminId, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.Equal("Acme Corp", result.Value!.Name);   // trimmed
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.Equal("Acme Corp", saved.Name);
        // A general-only update must not disturb the other groups.
        Assert.Equal(7_000_000, saved.MaxUploadBytes);
        Assert.False(saved.MarketplaceEnabled);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.general.updated", adminId, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task UpdateGeneral_RejectsABlankName()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId, Name = "Acme" });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.UpdateGeneralAsync(
            new UpdateGeneralDto { Name = "   " }, Guid.NewGuid(), "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("Organization name is required.", result.Error);
        // The blank update must not have overwritten the stored name.
        Assert.Equal("Acme", (await ctx.OrganizationSettings.SingleAsync()).Name);
    }

    [Fact]
    public async Task GetBranding_ReturnsTheStoredName()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId, Name = "Acme Corp" });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var branding = await svc.GetBrandingAsync();

        Assert.Equal("Acme Corp", branding.Name);
    }

    [Fact]
    public async Task GetBranding_WithoutARow_FallsBackToTheDefaultName()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        var branding = await svc.GetBrandingAsync();

        // A database predating the seed still shows a brand, not a blank.
        Assert.Equal(OrganizationSettings.DefaultName, branding.Name);
        Assert.Null(branding.LogoUrl);   // no custom logo -> the built-in one
    }

    [Fact]
    public async Task UpdateLogo_StoresTheImage_AndExposesItsUrl()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var svc = Create(ctx);

        var result = await svc.UpdateLogoAsync(ImageOf(), admin, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        var saved = await ctx.OrganizationSettings.SingleAsync();
        Assert.NotNull(saved.LogoFileId);
        Assert.Equal(1, _storage.Count);   // the bytes really went to storage
        // The URL points at the public logo endpoint and cache-busts on the file id.
        Assert.Equal($"/api/settings/logo?v={saved.LogoFileId}", result.Value!.LogoUrl);
        _audit.Verify(a => a.LogAsync(
            "admin.settings.logo.updated", admin, "admin@test.local",
            nameof(OrganizationSettings), It.IsAny<string>(), It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task UpdateLogo_RejectsANonImage()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var svc = Create(ctx);

        var result = await svc.UpdateLogoAsync(
            ImageOf("notes.txt", "text/plain"), admin, "admin@test.local", null);

        Assert.False(result.Succeeded);
        Assert.Equal("Logo must be an image.", result.Error);
        Assert.Equal(0, _storage.Count);   // nothing stored
    }

    [Fact]
    public async Task UpdateLogo_ReplacingAnExistingLogo_DeletesTheOldImage()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var svc = Create(ctx);
        var first = (await svc.UpdateLogoAsync(ImageOf("a.png"), admin, "admin@test.local", null)).Value!;
        var firstId = (await ctx.OrganizationSettings.SingleAsync()).LogoFileId;

        var result = await svc.UpdateLogoAsync(ImageOf("b.png"), admin, "admin@test.local", null);

        Assert.True(result.Succeeded);
        // The old logo must not linger — exactly one image remains in storage and the DB.
        Assert.Equal(1, _storage.Count);
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == firstId));
        Assert.Single(await ctx.FileAttachments.ToListAsync());
        _ = first;
    }

    [Fact]
    public async Task RemoveLogo_ClearsThePointer_AndDeletesTheImage()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var svc = Create(ctx);
        await svc.UpdateLogoAsync(ImageOf(), admin, "admin@test.local", null);

        var result = await svc.RemoveLogoAsync(admin, "admin@test.local", "127.0.0.1");

        Assert.True(result.Succeeded);
        Assert.Null((await ctx.OrganizationSettings.SingleAsync()).LogoFileId);
        Assert.Null(result.Value!.LogoUrl);
        Assert.Equal(0, _storage.Count);           // no orphaned bytes
        Assert.Equal(0, await ctx.FileAttachments.CountAsync());
    }

    [Fact]
    public async Task RemoveLogo_WhenNoneIsSet_IsANoOp()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.RemoveLogoAsync(Guid.NewGuid(), "admin@test.local", null);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.LogoUrl);
    }

    [Fact]
    public async Task GetLogo_ReturnsTheImageBytes_WhenSet_And404sOtherwise()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId });
        await ctx.SaveChangesAsync();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var svc = Create(ctx);

        // No logo yet.
        Assert.False((await svc.GetLogoAsync()).Succeeded);

        await svc.UpdateLogoAsync(ImageOf(), admin, "admin@test.local", null);
        var download = await svc.GetLogoAsync();

        Assert.True(download.Succeeded);
        Assert.Equal("image/png", download.Value!.ContentType);
    }
}
