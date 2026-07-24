using MassTransit;
using Taskpilot.Integrations;
using Taskpilot.NotificationService.Consumers;

// Generic worker host: no web server, just the MassTransit bus running as a hosted service.
var builder = Host.CreateApplicationBuilder(args);

// RabbitMQ connection, same convention as the API (RabbitMq:Connection, e.g.
// amqp://guest:guest@localhost:5672). The service has nothing to do without a broker, so a
// missing connection is a hard configuration error rather than a silent no-op.
var connection = builder.Configuration["RabbitMq:Connection"];
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException(
        "RabbitMq:Connection is required. Set it (e.g. RabbitMq__Connection=amqp://guest:guest@localhost:5672) before starting the notification service.");

// Notification senders + the shared dispatcher — the exact same delivery code the API runs
// inline. Config comes from the same Email/Telegram/Viber sections (env: Email__*, Telegram__*,
// Viber__*), so the service delivers identically whether it runs here or in the API.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<ViberOptions>(builder.Configuration.GetSection("Viber"));

// Prefer SMTP when a host is set, otherwise the SendGrid API sender (both no-op when unconfigured).
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetSection("Email")["SmtpHost"]))
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();

builder.Services.AddHttpClient<ITelegramSender, TelegramSender>();
builder.Services.AddHttpClient<IViberSender, ViberSender>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

builder.Services.AddMassTransit(x =>
{
    // Register the consumer; ConfigureEndpoints binds it to the message-type exchange the
    // API publishes to, so this service receives every NotificationDeliveryMessage.
    x.AddConsumer<NotificationDeliveryConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri(connection));
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
