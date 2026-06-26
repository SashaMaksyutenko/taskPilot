namespace Taskpilot.API.Services;

/// <summary>
/// Periodically scans for overdue tasks and emits notifications/webhooks. Runs on a
/// timer; resolves the scoped <see cref="IOverdueService"/> per run.
/// </summary>
public class OverdueBackgroundService : BackgroundService
{
    // How often to check. Short enough to be visible in a demo, fine for production too.
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueBackgroundService> _logger;

    public OverdueBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OverdueBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the app finishes starting up first.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var overdue = scope.ServiceProvider.GetRequiredService<IOverdueService>();
                await overdue.ProcessOverdueAsync();
            }
            catch (Exception ex)
            {
                // A failed run must not stop the loop.
                _logger.LogError(ex, "Overdue background check failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
