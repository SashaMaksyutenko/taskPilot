namespace Taskpilot.API.Services;

/// <summary>
/// Periodically mails out scheduled reports. Runs hourly; the per-schedule cadence
/// (daily/weekly/monthly) is enforced by <see cref="ReportScheduleService"/> via each
/// schedule's last-sent timestamp, so waking up hourly is safe and cheap.
/// </summary>
public class ReportScheduleBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportScheduleBackgroundService> _logger;

    public ReportScheduleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReportScheduleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting up before the first run.
        try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var schedules = scope.ServiceProvider.GetRequiredService<IReportScheduleService>();
                await schedules.SendDueAsync();
            }
            catch (Exception ex)
            {
                // A failed run must not stop the loop.
                _logger.LogError(ex, "Scheduled report run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
