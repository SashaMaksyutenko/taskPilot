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

    /// <summary>Uploaded file metadata.</summary>
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();

    /// <summary>Forum discussion topics.</summary>
    public DbSet<ForumTopic> ForumTopics => Set<ForumTopic>();

    /// <summary>Forum replies.</summary>
    public DbSet<ForumReply> ForumReplies => Set<ForumReply>();

    /// <summary>Votes on forum replies.</summary>
    public DbSet<ForumVote> ForumVotes => Set<ForumVote>();

    /// <summary>Public marketplace tasks.</summary>
    public DbSet<MarketplaceTask> MarketplaceTasks => Set<MarketplaceTask>();

    /// <summary>Applications to marketplace tasks.</summary>
    public DbSet<TaskApplication> TaskApplications => Set<TaskApplication>();

    /// <summary>In-app notifications.</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>Projects.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Tasks within projects.</summary>
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    /// <summary>Outgoing webhooks.</summary>
    public DbSet<Webhook> Webhooks => Set<Webhook>();

    /// <summary>Audit trail of actions performed in the system.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Two-way reviews for completed marketplace tasks.</summary>
    public DbSet<Review> Reviews => Set<Review>();

    /// <summary>Personal notes.</summary>
    public DbSet<Note> Notes => Set<Note>();

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

            // Optional profile fields.
            entity.Property(u => u.Title).HasMaxLength(100);
            entity.Property(u => u.Bio).HasMaxLength(1000);
            entity.Property(u => u.Location).HasMaxLength(100);

            // Optional contact / social links.
            entity.Property(u => u.Website).HasMaxLength(200);
            entity.Property(u => u.LinkedIn).HasMaxLength(200);
            entity.Property(u => u.GitHub).HasMaxLength(200);
            entity.Property(u => u.Phone).HasMaxLength(30);
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

            // Optional file attachment; keep the file row if the message is removed.
            entity.HasOne(m => m.FileAttachment)
                  .WithMany()
                  .HasForeignKey(m => m.FileAttachmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // FileAttachment entity configuration
        modelBuilder.Entity<FileAttachment>(entity =>
        {
            entity.HasKey(f => f.Id);

            entity.Property(f => f.FileName).IsRequired().HasMaxLength(260);
            entity.Property(f => f.StoredName).IsRequired().HasMaxLength(100);
            entity.Property(f => f.ContentType).IsRequired().HasMaxLength(150);

            // Keep files even if the uploader is removed (restrict).
            entity.HasOne(f => f.Uploader)
                  .WithMany()
                  .HasForeignKey(f => f.UploaderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ForumTopic entity configuration
        modelBuilder.Entity<ForumTopic>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Body).IsRequired().HasMaxLength(10000);

            // List newest/pinned topics quickly.
            entity.HasIndex(t => t.CreatedAt);

            entity.HasOne(t => t.Author)
                  .WithMany()
                  .HasForeignKey(t => t.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ForumReply entity configuration
        modelBuilder.Entity<ForumReply>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Body).IsRequired().HasMaxLength(10000);

            // Fetch a topic's replies in order.
            entity.HasIndex(r => new { r.TopicId, r.CreatedAt });

            // Deleting a topic removes its replies.
            entity.HasOne(r => r.Topic)
                  .WithMany(t => t.Replies)
                  .HasForeignKey(r => r.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Author and the optional parent reply are restricted to avoid
            // multiple cascade paths / self-reference cycles.
            entity.HasOne(r => r.Author)
                  .WithMany()
                  .HasForeignKey(r => r.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ParentReply)
                  .WithMany()
                  .HasForeignKey(r => r.ParentReplyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ForumVote entity configuration
        modelBuilder.Entity<ForumVote>(entity =>
        {
            entity.HasKey(v => v.Id);

            // One vote per user per reply.
            entity.HasIndex(v => new { v.ReplyId, v.UserId }).IsUnique();

            // Deleting a reply removes its votes.
            entity.HasOne(v => v.Reply)
                  .WithMany(r => r.Votes)
                  .HasForeignKey(v => v.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the user side to avoid multiple cascade paths to Users.
            entity.HasOne(v => v.User)
                  .WithMany()
                  .HasForeignKey(v => v.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // MarketplaceTask entity configuration
        modelBuilder.Entity<MarketplaceTask>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(10000);
            entity.Property(t => t.RequiredSkills).HasMaxLength(500);
            // Money: fixed precision instead of the provider default.
            entity.Property(t => t.Budget).HasPrecision(18, 2);

            // Store status as a readable string.
            entity.Property(t => t.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasIndex(t => t.CreatedAt);

            // Poster and (optional) assignee are restricted to avoid cascade paths to Users.
            entity.HasOne(t => t.Poster)
                  .WithMany()
                  .HasForeignKey(t => t.PosterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Assignee)
                  .WithMany()
                  .HasForeignKey(t => t.AssigneeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskApplication entity configuration
        modelBuilder.Entity<TaskApplication>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.CoverLetter).IsRequired().HasMaxLength(2000);
            entity.Property(a => a.ProposedRate).HasPrecision(18, 2);

            entity.Property(a => a.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // A developer can apply to a task only once.
            entity.HasIndex(a => new { a.TaskId, a.ApplicantId }).IsUnique();

            // Deleting a task removes its applications.
            entity.HasOne(a => a.Task)
                  .WithMany(t => t.Applications)
                  .HasForeignKey(a => a.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the applicant side to avoid multiple cascade paths to Users.
            entity.HasOne(a => a.Applicant)
                  .WithMany()
                  .HasForeignKey(a => a.ApplicantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Notification entity configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Message).IsRequired().HasMaxLength(500);
            entity.Property(n => n.Link).HasMaxLength(500);
            entity.Property(n => n.Type)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // Fast lookup of a user's notifications, newest first / unread filter.
            entity.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt });

            entity.HasOne(n => n.Recipient)
                  .WithMany()
                  .HasForeignKey(n => n.RecipientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Project entity configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Description).HasMaxLength(5000);
            entity.Property(p => p.Color).HasMaxLength(20);

            entity.HasIndex(p => p.OwnerId);

            entity.HasOne(p => p.Owner)
                  .WithMany()
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectTask entity configuration
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Description).HasMaxLength(10000);

            // Store status and priority as readable strings.
            entity.Property(t => t.Status)
                  .HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(t => t.Priority)
                  .HasConversion<string>().HasMaxLength(20).IsRequired();

            // Common queries: a project's tasks by status.
            entity.HasIndex(t => new { t.ProjectId, t.Status });

            // Deleting a project removes its tasks.
            entity.HasOne(t => t.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(t => t.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            // User/self links are restricted to avoid multiple cascade paths / cycles.
            entity.HasOne(t => t.Assignee)
                  .WithMany()
                  .HasForeignKey(t => t.AssigneeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Creator)
                  .WithMany()
                  .HasForeignKey(t => t.CreatorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ParentTask)
                  .WithMany()
                  .HasForeignKey(t => t.ParentTaskId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Webhook entity configuration
        modelBuilder.Entity<Webhook>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Url).IsRequired().HasMaxLength(500);
            entity.Property(w => w.Event).IsRequired().HasMaxLength(100);
            entity.Property(w => w.Secret).IsRequired().HasMaxLength(200);

            // Find active webhooks for an event quickly when dispatching.
            entity.HasIndex(w => new { w.Event, w.IsActive });

            // Deleting a user removes their webhooks (only cascade path to Users).
            entity.HasOne(w => w.Owner)
                  .WithMany()
                  .HasForeignKey(w => w.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog entity configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.ActorEmail).HasMaxLength(256);
            entity.Property(a => a.EntityType).HasMaxLength(100);
            entity.Property(a => a.EntityId).HasMaxLength(100);
            entity.Property(a => a.Details).HasMaxLength(2000);
            entity.Property(a => a.IpAddress).HasMaxLength(64);

            // Common queries: the audit feed newest-first, and per-actor history.
            entity.HasIndex(a => a.CreatedAt);
            entity.HasIndex(a => a.ActorId);

            // Intentionally NO foreign key to User: audit logs are immutable history
            // and must remain even after the actor's account is deleted.
        });

        // Review entity configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Comment).HasMaxLength(1000);

            // One review per rater per task.
            entity.HasIndex(r => new { r.MarketplaceTaskId, r.RaterId }).IsUnique();
            // Quick lookup of a user's received reviews (for their average rating).
            entity.HasIndex(r => r.RateeId);

            // Deleting a task removes its reviews. Rater/ratee are plain ids (no FK).
            entity.HasOne(r => r.MarketplaceTask)
                  .WithMany()
                  .HasForeignKey(r => r.MarketplaceTaskId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Note entity configuration
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Title).HasMaxLength(200);
            entity.Property(n => n.Content).HasMaxLength(10000);
            entity.Property(n => n.Color).HasMaxLength(20);

            // List a user's notes quickly.
            entity.HasIndex(n => n.OwnerId);

            // Notes are personal data: deleting the owner removes their notes.
            entity.HasOne(n => n.Owner)
                  .WithMany()
                  .HasForeignKey(n => n.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
