// Program.cs — application entry point for Taskpilot.API.
// Configures the web host, services (DI) and the HTTP pipeline (middleware).
// Current scope: base skeleton + database connection (EF Core + PostgreSQL).
// JWT, Serilog, Swagger, etc. are added in later sessions.

using System.Text;
using DotNetEnv;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

// Register application services. Scoped = one instance per HTTP request.
builder.Services.AddScoped<IAuthService, AuthService>();
// Token generation is stateless, so a singleton is fine.
builder.Services.AddSingleton<ITokenService, TokenService>();

// Later sessions will add: AutoMapper, SignalR, MassTransit, Redis, etc.

// Build the application from the configured services.
var app = builder.Build();

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

// Authentication must run before authorization so the user identity is known.
app.UseAuthentication();
app.UseAuthorization();

// Map attribute-routed controllers (e.g. POST /api/auth/register).
app.MapControllers();

// Run the application (starts listening for HTTP requests).
app.Run();
