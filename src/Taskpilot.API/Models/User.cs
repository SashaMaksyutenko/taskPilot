namespace Taskpilot.API.Models;

/// <summary>
/// Сутність користувача системи Taskpilot.
/// Зберігає облікові дані, роль та метадані профілю.
/// Відповідає таблиці "Users" у базі даних.
/// </summary>
public class User
{
    /// <summary>Унікальний ідентифікатор користувача (первинний ключ).</summary>
    public Guid Id { get; set; }

    /// <summary>Повне імʼя користувача (відображається в інтерфейсі).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email — використовується для входу. Має бути унікальним у системі.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Хеш пароля (BCrypt). Ніколи не зберігаємо пароль у відкритому вигляді.
    /// Може бути null для користувачів, що увійшли лише через OAuth (Google/GitHub/LinkedIn).
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>Роль користувача (RBAC). За замовчуванням — Developer.</summary>
    public Role Role { get; set; } = Role.Developer;

    /// <summary>
    /// Чи активний акаунт. false — заблокований/деактивований (вхід заборонено).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// For a temporary ban: UTC time the ban lifts (the account auto-reactivates on the
    /// next login attempt afterwards). null means a permanent ban or no ban.
    /// Only meaningful while <see cref="IsActive"/> is false.
    /// </summary>
    public DateTime? BannedUntil { get; set; }

    /// <summary>
    /// For a mute: UTC time the mute lifts. While in the future the user can read but
    /// not post (chat, forum, comments). null or past means not muted.
    /// </summary>
    public DateTime? MutedUntil { get; set; }

    /// <summary>
    /// Base32 TOTP secret for two-factor auth. Set once the user starts enrolling;
    /// cleared when 2FA is disabled. Null when never enrolled.
    /// </summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>True once the user has confirmed and enabled two-factor authentication.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Дата та час створення акаунта (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата та час останнього оновлення профілю (UTC).
    /// null, якщо запис ще жодного разу не оновлювався.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // --- Profile fields (optional) ---

    /// <summary>
    /// Id of the uploaded file used as the profile avatar; null when none set.
    /// The image itself is served publicly via GET /api/users/{id}/avatar.
    /// </summary>
    public Guid? AvatarFileId { get; set; }

    /// <summary>Job title / headline shown on the profile.</summary>
    public string? Title { get; set; }

    /// <summary>Short "about me" text.</summary>
    public string? Bio { get; set; }

    /// <summary>Location (city/country).</summary>
    public string? Location { get; set; }

    // --- Contact / social links (optional) ---

    /// <summary>Personal website URL.</summary>
    public string? Website { get; set; }

    /// <summary>LinkedIn profile URL.</summary>
    public string? LinkedIn { get; set; }

    /// <summary>GitHub profile URL.</summary>
    public string? GitHub { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Privacy toggle: whether the email is shown on the public profile.
    /// Defaults to false — email stays private unless the user opts in.
    /// </summary>
    public bool ShowEmail { get; set; }
}
