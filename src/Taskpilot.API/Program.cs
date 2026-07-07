// Program.cs — application entry point for Taskpilot.API.
// Configures the web host, services (DI) and the HTTP pipeline (middleware).
// Current scope: base skeleton + database connection (EF Core + PostgreSQL).
// JWT, Serilog, Swagger, etc. are added in later sessions.

using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Hubs;
using Taskpilot.API.Middleware;
using Taskpilot.API.Services;
using Taskpilot.API.Validators.Auth;
using Taskpilot.API.Workers;

// Load secrets from the local .env file into environment variables BEFORE the
// configuration is built. TraversePath() walks up the folder tree, so the file
// is found whether the app runs via `dotnet run` or from the bin output folder.
// The .env file is gitignored and never committed.
Env.TraversePath().Load();

// QuestPDF runs under its free Community license (allowed for this project size).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Build the host. Configuration is read from appsettings.json, environment
// variables (including everything loaded from .env above) and command-line args.
var builder = WebApplication.CreateBuilder(args);

// --- Service registration (Dependency Injection) ---

// The connection string lives only in .env (key: ConnectionStrings__DefaultConnection).
// .NET maps the "__" separator in env vars to the ":" config hierarchy.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Fail fast with a clear message if the connection string is not configured.
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "Copy .env.example to .env and set ConnectionStrings__DefaultConnection.");
}

// Register the EF Core database context using the Npgsql (PostgreSQL) provider.
// AddDbContext registers TaskpilotDbContext with a Scoped lifetime (one per request).
builder.Services.AddDbContext<TaskpilotDbContext>(options =>
    options.UseNpgsql(connectionString));

// CORS: allow the React dev frontend (http://localhost:5173) to call this API
// from the browser. In production this origin should come from configuration.
const string FrontendCorsPolicy = "frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              // Required for SignalR WebSocket connections from the browser.
              .AllowCredentials());
});

// Add MVC controllers (the project started as minimal API, so this is required
// for attribute-routed controllers like AuthController to be discovered).
builder.Services.AddControllers();

// Register all FluentValidation validators found in the assembly that contains
// RegisterValidator, so they can be injected (e.g. IValidator<RegisterDto>).
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

// Bind JWT settings from the "Jwt" config section (populated from .env: Jwt__*).
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Google OAuth credentials (populated from .env: GoogleOAuth__*).
builder.Services.Configure<GoogleOAuthOptions>(builder.Configuration.GetSection("GoogleOAuth"));
builder.Services.AddHttpClient<IGoogleAuthClient, GoogleAuthClient>();

// GitHub OAuth credentials (populated from .env: GitHubOAuth__*).
builder.Services.Configure<GitHubOAuthOptions>(builder.Configuration.GetSection("GitHubOAuth"));
builder.Services.AddHttpClient<IGitHubAuthClient, GitHubAuthClient>();

// Distributed cache: use Redis when a connection string is configured (Redis__Connection),
// otherwise an in-memory cache so the app runs the same without Redis installed.
var redisConnection = builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(redisConnection))
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redisConnection;
        o.InstanceName = "taskpilot:";
    });
else
    builder.Services.AddDistributedMemoryCache();

// Web Push (VAPID keys from .env: Vapid__*). No keys = push disabled.
builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddScoped<IPushService, PushService>();
// Dev convenience: print a fresh VAPID pair to copy into .env when none is set.
if (string.IsNullOrWhiteSpace(builder.Configuration["Vapid:PublicKey"]))
{
    var keys = WebPush.VapidHelper.GenerateVapidKeys();
    Console.WriteLine("[Web Push] VAPID not configured. Add this pair to src/Taskpilot.API/.env:");
    Console.WriteLine($"  Vapid__PublicKey={keys.PublicKey}");
    Console.WriteLine($"  Vapid__PrivateKey={keys.PrivateKey}");
}

// Telegram bot (populated from .env: Telegram__*). No token = bot disabled.
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient<ITelegramSender, TelegramSender>();
builder.Services.AddScoped<ITelegramLinkService, TelegramLinkService>();
builder.Services.AddHostedService<TelegramPollingService>();

// Email delivery — populated from .env: Email__*. Prefer SMTP (Gmail/Brevo/…) when
// an SMTP host is set; otherwise fall back to the SendGrid API sender. No config =
// both are disabled and email is silently skipped.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
var emailSection = builder.Configuration.GetSection("Email");
if (!string.IsNullOrWhiteSpace(emailSection["SmtpHost"]))
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();

// Bind the initial-admin credentials (populated from .env: Admin__*).
builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("Admin"));

// Read the JWT settings now so we can configure token validation below.
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured. Set Jwt__* in .env.");

// Configure JWT bearer authentication: incoming "Authorization: Bearer <token>"
// headers are validated against the same key/issuer/audience used to issue tokens.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep original claim names (e.g. "sub") instead of remapping them.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            // No tolerance window: a token is invalid the moment it expires.
            ClockSkew = TimeSpan.Zero
        };

        // Browsers cannot set the Authorization header on a WebSocket handshake,
        // so for SignalR hub paths read the token from the "access_token" query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Enables [Authorize] attributes on controllers/actions.
builder.Services.AddAuthorization();

// Swagger / OpenAPI: interactive API docs and a test UI at /swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Taskpilot API", Version = "v1" });

    // Add a "Bearer" auth scheme so protected endpoints (e.g. /me) can be tested
    // by pasting a JWT access token into the Swagger "Authorize" dialog.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT access token (Swagger adds the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Real-time messaging (SignalR) — powers the chat hub.
builder.Services.AddSignalR();

// Tracks who is currently connected to the chat hub (shared singleton state).
builder.Services.AddSingleton<PresenceTracker>();

// Tracks anonymous (not-logged-in) site visitors (shared singleton state).
builder.Services.AddSingleton<VisitorTracker>();

// Register application services. Scoped = one instance per HTTP request.
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IWarningService, WarningService>();
builder.Services.AddScoped<IAppealService, AppealService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();
builder.Services.AddScoped<ITaskCommentService, TaskCommentService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IOverdueService, OverdueService>();
// Background worker that flags overdue tasks (notifications + webhooks).
builder.Services.AddHostedService<OverdueBackgroundService>();
// HttpClient factory used to deliver webhook POSTs.
builder.Services.AddHttpClient();
// Token generation is stateless, so a singleton is fine.
builder.Services.AddSingleton<ITokenService, TokenService>();

// Rate limiting protects abusable endpoints from brute force and spam.
// The "auth" policy allows max 5 requests per minute per client IP; once the
// window is exceeded the request is rejected with 429 Too Many Requests.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Partition by caller IP so one client cannot exhaust everyone's quota.
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0, // reject immediately instead of queueing
            }));

    // Global limit applied to every request: max 100 per minute, partitioned by
    // authenticated user (JWT "sub") so the quota follows the person across IPs,
    // falling back to the caller IP for anonymous requests. Real-time hub paths
    // and the health probe are exempt so long-lived connections and monitoring
    // are never throttled.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        if (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("exempt");

        // "sub" is the user-id claim (MapInboundClaims is disabled, so it stays "sub").
        var partitionKey = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

// Later sessions will add: AutoMapper, MassTransit, Redis, etc.

// Build the application from the configured services.
var app = builder.Build();

// Apply any pending EF Core migrations on startup. This creates/updates the
// schema automatically in fresh environments (e.g. a container or a new server),
// so no separate migration step is needed to deploy.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskpilotDbContext>();
    await db.Database.MigrateAsync();
}

// Ensure the initial admin user exists (created/promoted from Admin__* in .env).
await DataSeeder.SeedAdminAsync(app.Services);

// --- HTTP pipeline (middleware) ---

// Expose Swagger UI in Development only (browse and test endpoints at /swagger).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Taskpilot API v1");
        // Serve the UI at the app root so http://localhost:<port>/ opens Swagger.
        options.RoutePrefix = "swagger";
    });
}

// Root endpoint: simple liveness response.
app.MapGet("/", () => "Taskpilot API is running");

// Health-check endpoint for monitoring (returns status and server time in UTC).
app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// CORS must run before authentication/authorization and endpoint routing.
app.UseCors(FrontendCorsPolicy);

// Authentication must run before authorization so the user identity is known.
app.UseAuthentication();
app.UseAuthorization();

// Enforce the configured rate-limiting policies (e.g. the "auth" policy).
app.UseRateLimiter();

// Count anonymous visitors (runs after authentication so we know who is logged in).
app.UseMiddleware<VisitorTrackingMiddleware>();

// RBAC: make the Viewer role read-only (runs after authentication so the user's
// role is known). Must precede endpoint execution.
app.UseMiddleware<ViewerReadOnlyMiddleware>();

// Map attribute-routed controllers (e.g. POST /api/auth/register).
app.MapControllers();

// Map the SignalR hubs. Clients connect for real-time messages and notifications.
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<TaskHub>("/hubs/tasks");

// Run the application (starts listening for HTTP requests).
app.Run();
