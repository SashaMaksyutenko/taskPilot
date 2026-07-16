namespace Taskpilot.API.Services;

/// <summary>
/// Periodically triggers digest email delivery. Runs hourly; the per-user cadence
/// (daily/weekly) is enforced by <see cref="DigestService"/> via each user's
/// last-sent timestamp, so waking up hourly is safe and cheap.
/// </summary>
public class DigestBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestBackgroundService> _logger;

    public DigestBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DigestBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting up before the first run.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var digest = scope.ServiceProvider.GetRequiredService<IDigestService>();
                await digest.SendDueDigestsAsync();
            }
            catch (Exception ex)
            {
                // A failed run must not stop the loop.
                _logger.LogError(ex, "Digest background run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
