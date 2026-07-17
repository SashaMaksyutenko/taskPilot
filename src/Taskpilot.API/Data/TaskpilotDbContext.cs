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

    /// <summary>Emoji reactions on forum replies.</summary>
    public DbSet<ForumReplyReaction> ForumReplyReactions => Set<ForumReplyReaction>();

    /// <summary>User subscriptions to forum topics.</summary>
    public DbSet<ForumTopicSubscription> ForumTopicSubscriptions => Set<ForumTopicSubscription>();

    /// <summary>User reports of forum replies to moderators.</summary>
    public DbSet<ForumReport> ForumReports => Set<ForumReport>();

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
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    /// <summary>Outgoing webhooks.</summary>
    public DbSet<Webhook> Webhooks => Set<Webhook>();

    /// <summary>Audit trail of actions performed in the system.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Two-way reviews for completed marketplace tasks.</summary>
    public DbSet<Review> Reviews => Set<Review>();

    /// <summary>Personal notes.</summary>
    public DbSet<Note> Notes => Set<Note>();

    /// <summary>Users' bookmarks (saved shortcuts to tasks/topics/messages).</summary>
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    /// <summary>Reputation ledger: persisted history of point-affecting events.</summary>
    public DbSet<ReputationEntry> ReputationEntries => Set<ReputationEntry>();

    /// <summary>Task deadline-extension requests awaiting/holding owner decisions.</summary>
    public DbSet<TaskExtensionRequest> TaskExtensionRequests => Set<TaskExtensionRequest>();

    /// <summary>Users' saved search queries.</summary>
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();

    /// <summary>Delivery log for outgoing webhooks (status, retries, errors).</summary>
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    /// <summary>Recurring report emails users have scheduled.</summary>
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    /// <summary>Comments on project tasks.</summary>
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    /// <summary>Moderation warnings issued to users.</summary>
    public DbSet<UserWarning> UserWarnings => Set<UserWarning>();

    /// <summary>Appeals against moderation warnings.</summary>
    public DbSet<Appeal> Appeals => Set<Appeal>();

    /// <summary>Per-user notification type opt-outs.</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    /// <summary>Project collaborators (shared access).</summary>
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    /// <summary>Two-factor recovery codes.</summary>
    public DbSet<UserBackupCode> UserBackupCodes => Set<UserBackupCode>();

    /// <summary>Emoji reactions on chat messages.</summary>
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    /// <summary>Personal API keys for programmatic access.</summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    /// <summary>Persisted anonymous-visitor analytics (per day, per hashed IP).</summary>
    public DbSet<VisitorHit> VisitorHits => Set<VisitorHit>();

    /// <summary>One-time password-reset tokens (hashed).</summary>
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>Organization-wide settings (a single seeded row).</summary>
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();

    /// <summary>
    /// Налаштування моделі (Fluent API): обмеження, індекси, перетворення типів.
    /// Викликається EF Core під час побудови моделі та генерації міграцій.
    /// </summary>
    /// <param name="modelBuilder">Будівник моделі для конфігурації сутностей.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One row per unique (day, IP-hash) so unique-visitor counts stay accurate.
        modelBuilder.Entity<VisitorHit>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => new { v.Day, v.IpHash }).IsUnique();
        });

        // Organization settings: a single seeded row so a fresh database always has the
        // default limits, and reads never have to cope with an empty table.
        modelBuilder.Entity<OrganizationSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasData(new OrganizationSettings
            {
                Id = Models.OrganizationSettings.SingletonId,
                MaxUploadBytes = Models.OrganizationSettings.DefaultMaxUploadBytes,
                StorageQuotaBytes = Models.OrganizationSettings.DefaultStorageQuotaBytes,
                UpdatedAt = null,
            });
        });

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

            // Digest cadence stored as a readable string ("Off"/"Daily"/"Weekly").
            entity.Property(u => u.DigestFrequency)
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

            // Public share token: unique when set, looked up on anonymous download.
            entity.Property(f => f.ShareToken).HasMaxLength(64);
            entity.HasIndex(f => f.ShareToken).IsUnique();

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

            // Tags are stored as a Postgres text[] and never null.
            entity.Property(t => t.Tags)
                  .HasColumnType("text[]")
                  .HasDefaultValueSql("'{}'::text[]");

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

        // ForumReplyReaction entity configuration
        modelBuilder.Entity<ForumReplyReaction>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Emoji).IsRequired().HasMaxLength(16);

            // One row per (reply, user, emoji) — toggling adds/removes it.
            entity.HasIndex(r => new { r.ReplyId, r.UserId, r.Emoji }).IsUnique();

            // Deleting a reply removes its reactions.
            entity.HasOne(r => r.Reply)
                  .WithMany(rep => rep.Reactions)
                  .HasForeignKey(r => r.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the user side to avoid multiple cascade paths to Users.
            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ForumTopicSubscription entity configuration
        modelBuilder.Entity<ForumTopicSubscription>(entity =>
        {
            entity.HasKey(s => s.Id);

            // One subscription per user per topic.
            entity.HasIndex(s => new { s.TopicId, s.UserId }).IsUnique();

            // Deleting a topic removes its subscriptions.
            entity.HasOne(s => s.Topic)
                  .WithMany()
                  .HasForeignKey(s => s.TopicId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the user side to avoid multiple cascade paths to Users.
            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ForumReport entity configuration
        modelBuilder.Entity<ForumReport>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Reason).HasMaxLength(1000);

            // Store status as a readable string.
            entity.Property(r => r.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasIndex(r => new { r.Status, r.CreatedAt });

            // Deleting a reply removes its reports.
            entity.HasOne(r => r.Reply)
                  .WithMany()
                  .HasForeignKey(r => r.ReplyId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the user side to avoid multiple cascade paths to Users.
            entity.HasOne(r => r.Reporter)
                  .WithMany()
                  .HasForeignKey(r => r.ReporterId)
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

        // PushSubscription entity configuration
        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Endpoint).IsRequired();
            // One row per browser subscription endpoint.
            entity.HasIndex(s => s.Endpoint).IsUnique();
            entity.HasIndex(s => s.UserId);
            // Deleting a user removes their push subscriptions.
            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
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

            // Tags are stored as a Postgres text[] and never null.
            entity.Property(t => t.Tags)
                  .HasColumnType("text[]")
                  .HasDefaultValueSql("'{}'::text[]");

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

            // Tags are stored as a Postgres text[] and never null.
            entity.Property(n => n.Tags)
                  .HasColumnType("text[]")
                  .HasDefaultValueSql("'{}'::text[]");

            // List a user's notes quickly.
            entity.HasIndex(n => n.OwnerId);

            // Notes are personal data: deleting the owner removes their notes.
            entity.HasOne(n => n.Owner)
                  .WithMany()
                  .HasForeignKey(n => n.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Bookmark entity configuration
        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Title).HasMaxLength(300);
            entity.Property(b => b.Link).HasMaxLength(500);

            // Store the type as a readable string.
            entity.Property(b => b.Type)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // One bookmark per user per entity; newest first per user.
            entity.HasIndex(b => new { b.UserId, b.Type, b.EntityId }).IsUnique();
            entity.HasIndex(b => new { b.UserId, b.CreatedAt });

            // Bookmarks are personal data: deleting the owner removes them.
            entity.HasOne(b => b.User)
                  .WithMany()
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ReputationEntry (ledger) configuration
        modelBuilder.Entity<ReputationEntry>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Description).HasMaxLength(300);

            // Store the reason as a readable string.
            entity.Property(e => e.Reason)
                  .HasConversion<string>()
                  .HasMaxLength(30)
                  .IsRequired();

            // History is read per user, newest first.
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            // Ledger is personal data: deleting the user removes their history.
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ReportSchedule configuration
        modelBuilder.Entity<ReportSchedule>(entity =>
        {
            entity.HasKey(s => s.Id);

            // Enums stored as readable strings.
            entity.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(s => s.Format).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.HasIndex(s => new { s.UserId, s.ProjectId });

            // A schedule is personal and project-bound: it dies with either.
            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Project)
                  .WithMany()
                  .HasForeignKey(s => s.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // WebhookDelivery (delivery log) configuration
        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Event).IsRequired().HasMaxLength(60);
            entity.Property(d => d.Error).HasMaxLength(500);

            // Read per webhook, newest first.
            entity.HasIndex(d => new { d.WebhookId, d.CreatedAt });

            // Deleting a webhook removes its delivery history.
            entity.HasOne(d => d.Webhook)
                  .WithMany()
                  .HasForeignKey(d => d.WebhookId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SavedSearch configuration
        modelBuilder.Entity<SavedSearch>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Query).IsRequired().HasMaxLength(200);

            // Read per user, newest first.
            entity.HasIndex(s => new { s.UserId, s.CreatedAt });

            // Saved searches are personal: deleting the user removes them.
            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskExtensionRequest configuration
        modelBuilder.Entity<TaskExtensionRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Reason).HasMaxLength(1000);

            // Status stored as a readable string.
            entity.Property(r => r.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // Requests are read per task, newest first.
            entity.HasIndex(r => new { r.TaskId, r.CreatedAt });

            // Remove a task's requests with the task.
            entity.HasOne(r => r.Task)
                  .WithMany()
                  .HasForeignKey(r => r.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Keep the requester link but don't cascade-delete requests with the user.
            entity.HasOne(r => r.Requester)
                  .WithMany()
                  .HasForeignKey(r => r.RequesterId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskComment entity configuration
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Body).IsRequired().HasMaxLength(5000);

            // List a task's comments in order.
            entity.HasIndex(c => new { c.TaskId, c.CreatedAt });

            // Deleting a task removes its comments.
            entity.HasOne(c => c.Task)
                  .WithMany()
                  .HasForeignKey(c => c.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Author link is restricted to avoid multiple cascade paths.
            entity.HasOne(c => c.Author)
                  .WithMany()
                  .HasForeignKey(c => c.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // UserWarning entity configuration
        modelBuilder.Entity<UserWarning>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Reason).IsRequired().HasMaxLength(1000);

            // List a user's warnings, newest first.
            entity.HasIndex(w => new { w.UserId, w.CreatedAt });

            // Deleting the warned user removes their warnings.
            entity.HasOne(w => w.User)
                  .WithMany()
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Issuer link is restricted to avoid multiple cascade paths.
            entity.HasOne(w => w.IssuedBy)
                  .WithMany()
                  .HasForeignKey(w => w.IssuedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Appeal entity configuration
        modelBuilder.Entity<Appeal>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Message).IsRequired().HasMaxLength(2000);
            entity.Property(a => a.ReviewNote).HasMaxLength(1000);
            entity.Property(a => a.Status)
                  .HasConversion<string>().HasMaxLength(20).IsRequired();

            // List a user's appeals + the admin's pending queue.
            entity.HasIndex(a => new { a.UserId, a.CreatedAt });
            entity.HasIndex(a => a.Status);

            // Deleting the user removes their appeals.
            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Approving an appeal deletes the warning; keep the appeal but null the link.
            entity.HasOne(a => a.Warning)
                  .WithMany()
                  .HasForeignKey(a => a.WarningId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Reviewer link is restricted to avoid multiple cascade paths.
            entity.HasOne(a => a.ReviewedBy)
                  .WithMany()
                  .HasForeignKey(a => a.ReviewedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectMember entity configuration
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Role)
                  .HasConversion<string>().HasMaxLength(20).IsRequired();

            // One membership per (project, user); also the access lookup.
            entity.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();

            // Deleting the project removes its memberships.
            entity.HasOne(m => m.Project)
                  .WithMany(p => p.Members)
                  .HasForeignKey(m => m.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            // User link is restricted to avoid multiple cascade paths.
            entity.HasOne(m => m.User)
                  .WithMany()
                  .HasForeignKey(m => m.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // MessageReaction entity configuration
        modelBuilder.Entity<MessageReaction>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Emoji).IsRequired().HasMaxLength(16);

            // One reaction per (message, user, emoji); also the lookup for a message.
            entity.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji }).IsUnique();

            // Deleting a message removes its reactions.
            entity.HasOne(r => r.Message)
                  .WithMany(m => m.Reactions)
                  .HasForeignKey(r => r.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);

            // User link is restricted to avoid multiple cascade paths.
            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // UserBackupCode entity configuration
        modelBuilder.Entity<UserBackupCode>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);

            entity.HasIndex(c => c.UserId);

            // Deleting the user removes their backup codes.
            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // NotificationPreference entity configuration
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Type)
                  .HasConversion<string>().HasMaxLength(20).IsRequired();

            // One opt-out row per (user, type); also the lookup used when sending.
            entity.HasIndex(p => new { p.UserId, p.Type }).IsUnique();

            // Deleting the user removes their preferences.
            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
