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

    /// <summary>Дата та час створення акаунта (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата та час останнього оновлення профілю (UTC).
    /// null, якщо запис ще жодного разу не оновлювався.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // --- Profile fields (optional) ---

    /// <summary>Job title / headline shown on the profile.</summary>
    public string? Title { get; set; }

    /// <summary>Short "about me" text.</summary>
    public string? Bio { get; set; }

    /// <summary>Location (city/country).</summary>
    public string? Location { get; set; }
}
