// Program.cs — application entry point for Taskpilot.API.
// Configures the web host, services (DI) and the HTTP pipeline (middleware).
// Current scope: base skeleton + database connection (EF Core + PostgreSQL).
// JWT, Serilog, Swagger, etc. are added in later sessions.

using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

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

// Later sessions will add: FluentValidation, AutoMapper,
// JWT authentication, SignalR, MassTransit, Redis, etc.

// Build the application from the configured services.
var app = builder.Build();

// --- HTTP pipeline (middleware) ---

// Root endpoint: simple liveness response.
app.MapGet("/", () => "Taskpilot API is running");

// Health-check endpoint for monitoring (returns status and server time in UTC).
app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// Run the application (starts listening for HTTP requests).
app.Run();
