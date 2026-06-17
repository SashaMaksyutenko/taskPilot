// Program.cs — application entry point for Taskpilot.API.
// Configures the web host, services (DI) and the HTTP pipeline (middleware).
// Current scope: base skeleton + database connection (EF Core + PostgreSQL).
// JWT, Serilog, Swagger, etc. are added in later sessions.

using DotNetEnv;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using Taskpilot.API.Validators.Auth;

// Load secrets from the local .env file into environment variables BEFORE the
// configuration is built. TraversePath() walks up the folder tree, so the file
// is found whether the app runs via `dotnet run` or from the bin output folder.
// The .env file is gitignored and never committed.
Env.TraversePath().Load();

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

// Add MVC controllers (the project started as minimal API, so this is required
// for attribute-routed controllers like AuthController to be discovered).
builder.Services.AddControllers();

// Register all FluentValidation validators found in the assembly that contains
// RegisterValidator, so they can be injected (e.g. IValidator<RegisterDto>).
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

// Bind JWT settings from the "Jwt" config section (populated from .env: Jwt__*).
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Register application services. Scoped = one instance per HTTP request.
builder.Services.AddScoped<IAuthService, AuthService>();
// Token generation is stateless, so a singleton is fine.
builder.Services.AddSingleton<ITokenService, TokenService>();

// Later sessions will add: AutoMapper, JWT authentication middleware,
// SignalR, MassTransit, Redis, etc.

// Build the application from the configured services.
var app = builder.Build();

// --- HTTP pipeline (middleware) ---

// Root endpoint: simple liveness response.
app.MapGet("/", () => "Taskpilot API is running");

// Health-check endpoint for monitoring (returns status and server time in UTC).
app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// Map attribute-routed controllers (e.g. POST /api/auth/register).
app.MapControllers();

// Run the application (starts listening for HTTP requests).
app.Run();
