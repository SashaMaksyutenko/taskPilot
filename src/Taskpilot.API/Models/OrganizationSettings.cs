namespace Taskpilot.API.Models;

/// <summary>
/// Organization-wide settings. TaskPilot is single-tenant, so there is exactly ONE row,
/// identified by <see cref="SingletonId"/> and seeded by the migration. Admins edit it at
/// runtime; nothing here lives in configuration, because a redeploy must not be needed to
/// change a limit.
///
/// This session holds only the storage limits that are actually enforced. Presentational
/// org settings (name, logo, timezone, feature toggles) belong here too but are added when
/// something enforces them — an unenforced setting is just a form that lies.
/// </summary>
public class OrganizationSettings
{
    /// <summary>
    /// The one and only row's fixed primary key. Using a constant makes the row a true
    /// singleton: the seed, every read and every update all target this id.
    /// </summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-0000000005e7");

    /// <summary>Default organization name shown across the app when none is configured.</summary>
    public const string DefaultName = "TaskPilot";

    /// <summary>Default largest allowed upload: 10 MB (the product spec's per-file cap).</summary>
    public const long DefaultMaxUploadBytes = 10L * 1024 * 1024;

    /// <summary>Default total storage the whole organization may use: 1 GB (per the spec).</summary>
    public const long DefaultStorageQuotaBytes = 1024L * 1024 * 1024;

    /// <summary>Primary key; always <see cref="SingletonId"/>.</summary>
    public Guid Id { get; set; } = SingletonId;

    /// <summary>
    /// Organization name. Shown in the app shell (sidebar/top bar), on the sign-in and
    /// landing pages, and in notification email subjects/footers in place of "TaskPilot".
    /// </summary>
    public string Name { get; set; } = DefaultName;

    /// <summary>
    /// Custom organization logo image, stored as a <see cref="FileAttachment"/>; null uses
    /// the built-in logo. A bare id with no foreign key (like <c>User.AvatarFileId</c>), so
    /// the old image is cleaned up by hand when the logo is replaced or removed.
    /// </summary>
    public Guid? LogoFileId { get; set; }

    /// <summary>Largest size, in bytes, a single uploaded file may be.</summary>
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;

    /// <summary>
    /// Total bytes every uploaded file may occupy across the organization. A new upload is
    /// rejected when it would push the sum of all stored files over this.
    /// </summary>
    public long StorageQuotaBytes { get; set; } = DefaultStorageQuotaBytes;

    /// <summary>
    /// Whether the public task Marketplace is available. When off, the whole
    /// <c>/api/marketplace</c> surface is blocked and the UI hides its entry point.
    /// </summary>
    public bool MarketplaceEnabled { get; set; } = true;

    /// <summary>
    /// Whether the discussion Forum is available. When off, the whole <c>/api/forum</c>
    /// surface is blocked and the UI hides its entry point.
    /// </summary>
    public bool ForumEnabled { get; set; } = true;

    /// <summary>
    /// Comma-separated email domains allowed to self-register (e.g. "acme.com, acme.io").
    /// Empty means registration is open to any domain. Enforced in
    /// <c>AuthService.RegisterAsync</c> via <see cref="Common.EmailDomainList"/>.
    /// </summary>
    public string AllowedEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated email domains barred from registering (e.g. "spam.example").
    /// Empty blocks nothing. Checked BEFORE the allowlist, so a blocked domain is refused
    /// even if it would otherwise be allowed.
    /// </summary>
    public string BlockedEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// Largest number of ACTIVE user accounts allowed. Zero means unlimited. When the
    /// organization is full, self-registration is refused (existing users are unaffected).
    /// </summary>
    public int MaxMembers { get; set; }

    /// <summary>UTC time the settings were last changed (null until first edited).</summary>
    public DateTime? UpdatedAt { get; set; }
}
