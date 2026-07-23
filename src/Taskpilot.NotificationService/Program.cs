using MassTransit;
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

builder.Services.AddMassTransit(x =>
{
    // Register the consumer; ConfigureEndpoints binds it to the message-type exchange the
    // API publishes to, so this service receives a copy of every NotificationDeliveryMessage.
    x.AddConsumer<NotificationDeliveryConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri(connection));
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
