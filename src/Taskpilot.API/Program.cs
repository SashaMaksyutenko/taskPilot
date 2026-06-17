// Program.cs — точка входу застосунку Taskpilot.API.
// Тут конфігурується веб-хост, сервіси (DI) та HTTP-конвеєр (middleware).
// На цьому етапі: базовий каркас + підключення бази даних (EF Core + PostgreSQL).
// JWT, Serilog, Swagger тощо додаються в наступних сесіях.

using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

// Створюємо будівник застосунку: читає appsettings.json, змінні середовища, аргументи
var builder = WebApplication.CreateBuilder(args);

// --- Реєстрація сервісів (Dependency Injection) ---

// Рядок підключення до PostgreSQL береться з appsettings.json (секція ConnectionStrings)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Реєструємо контекст БД EF Core з провайдером Npgsql (PostgreSQL).
// AddDbContext додає TaskpilotDbContext у контейнер DI з часом життя Scoped (один на запит).
builder.Services.AddDbContext<TaskpilotDbContext>(options =>
    options.UseNpgsql(connectionString));

// Сюди в наступних сесіях додамо: FluentValidation, AutoMapper,
// автентифікацію JWT, SignalR, MassTransit, Redis тощо.

// Збираємо застосунок із налаштованих сервісів
var app = builder.Build();

// --- HTTP-конвеєр (middleware) ---

// Кореневий ендпоінт: проста відповідь, що сервіс живий
app.MapGet("/", () => "Taskpilot API is running");

// Health-check ендпоінт для моніторингу (повертає статус і час сервера у форматі UTC)
app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

// Запускаємо застосунок (починає слухати HTTP-запити)
app.Run();
