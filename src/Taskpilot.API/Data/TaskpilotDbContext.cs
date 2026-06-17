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

    /// <summary>Refresh tokens table. Each row is one <see cref="RefreshToken"/>.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Chat conversations (direct and group).</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Conversation membership (which user is in which conversation).</summary>
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    /// <summary>Chat messages.</summary>
    public DbSet<Message> Messages => Set<Message>();

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

        // RefreshToken entity configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            // Primary key
            entity.HasKey(rt => rt.Id);

            // Token value: required, capped length, and unique so lookups are fast and safe
            entity.Property(rt => rt.Token)
                  .IsRequired()
                  .HasMaxLength(256);
            entity.HasIndex(rt => rt.Token)
                  .IsUnique();

            // Each refresh token belongs to exactly one user.
            // Deleting a user cascades to (removes) their refresh tokens.
            entity.HasOne(rt => rt.User)
                  .WithMany()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Conversation entity configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(c => c.Id);

            // Store the type as a readable string ("Direct"/"Group").
            entity.Property(c => c.Type)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // Name is optional (only group chats have one).
            entity.Property(c => c.Name)
                  .HasMaxLength(150);
        });

        // ConversationParticipant entity configuration
        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(p => p.Id);

            // A user can appear in a conversation only once.
            entity.HasIndex(p => new { p.ConversationId, p.UserId })
                  .IsUnique();

            // Removing a conversation removes its membership rows.
            entity.HasOne(p => p.Conversation)
                  .WithMany(c => c.Participants)
                  .HasForeignKey(p => p.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the user side to avoid multiple cascade paths to Users.
            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Message entity configuration
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Content)
                  .IsRequired()
                  .HasMaxLength(4000);

            // Fast lookup of a conversation's messages in chronological order.
            entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });

            // Deleting a conversation removes its messages.
            entity.HasOne(m => m.Conversation)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the sender side to avoid multiple cascade paths to Users.
            entity.HasOne(m => m.Sender)
                  .WithMany()
                  .HasForeignKey(m => m.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
