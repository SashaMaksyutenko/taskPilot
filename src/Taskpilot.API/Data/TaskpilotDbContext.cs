using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Models;

namespace Taskpilot.API.Data;

/// <summary>
/// Контекст бази даних EF Core для Taskpilot.
/// Є мостом між C#-моделями та таблицями PostgreSQL:
/// надає доступ до даних і описує конфігурацію схеми.
/// </summary>
public class TaskpilotDbContext : DbContext
{
    /// <summary>
    /// Конструктор. Параметри підключення (рядок підключення, провайдер)
    /// передаються через DI з Program.cs.
    /// </summary>
    /// <param name="options">Налаштування контексту (провайдер БД, рядок підключення).</param>
    public TaskpilotDbContext(DbContextOptions<TaskpilotDbContext> options)
        : base(options)
    {
    }

    /// <summary>Таблиця користувачів. Кожен рядок — один <see cref="User"/>.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Налаштування моделі (Fluent API): обмеження, індекси, перетворення типів.
    /// Викликається EF Core під час побудови моделі та генерації міграцій.
    /// </summary>
    /// <param name="modelBuilder">Будівник моделі для конфігурації сутностей.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфігурація сутності User
        modelBuilder.Entity<User>(entity =>
        {
            // Первинний ключ
            entity.HasKey(u => u.Id);

            // Імʼя: обовʼязкове, максимум 100 символів
            entity.Property(u => u.Name)
                  .IsRequired()
                  .HasMaxLength(100);

            // Email: обовʼязковий, максимум 256 символів
            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(256);

            // Унікальний індекс на Email — два користувачі не можуть мати однаковий email
            entity.HasIndex(u => u.Email)
                  .IsUnique();

            // Хеш пароля: довжина BCrypt-хешу фіксована (~60 символів), беремо із запасом
            entity.Property(u => u.PasswordHash)
                  .HasMaxLength(255);

            // Роль зберігаємо як рядок ("Developer", "Manager"...), а не число —
            // так дані в БД читабельні й стійкі до зміни порядку значень enum
            entity.Property(u => u.Role)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();
        });
    }
}
