using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;

namespace Taskpilot.API.Workers;

/// <summary>
/// Background worker that reclaims expired demo accounts (and their sample data) once an hour.
/// Does nothing when the demo is turned off.
/// </summary>
public class DemoCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DemoOptions _options;
    private readonly ILogger<DemoCleanupService> _logger;

    public DemoCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<DemoOptions> options,
        ILogger<DemoCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Demo mode off; cleanup disabled.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var demo = scope.ServiceProvider.GetRequiredService<IDemoService>();
                await demo.PurgeExpiredAsync();
            }
            catch (Exception ex)
            {
                // A failed sweep must never crash the host — just log and try again next tick.
                _logger.LogError(ex, "Demo cleanup sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
