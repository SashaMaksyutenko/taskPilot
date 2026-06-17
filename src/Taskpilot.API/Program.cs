// Program.cs — точка входу застосунку Taskpilot.API.
// Тут конфігурується веб-хост, сервіси (DI) та HTTP-конвеєр (middleware).
// На цьому етапі лише базовий каркас: підключення сервісів і ендпоінти
// (база даних, JWT, Serilog, Swagger тощо додаються в наступних сесіях).

// Створюємо будівник застосунку: читає appsettings.json, змінні середовища, аргументи
var builder = WebApplication.CreateBuilder(args);

// --- Реєстрація сервісів (Dependency Injection) ---
// Сюди в наступних сесіях додамо: DbContext, FluentValidation, AutoMapper,
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
