using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Expiration;

namespace AspNetCore_net6._0_TestApp.Services;

public sealed class ExpiredFilesCleanupService : IHostedService, IDisposable
{
    private readonly ITusExpirationStore _expirationStore;
    private readonly ExpirationBase _expiration;
    private readonly ILogger<ExpiredFilesCleanupService> _logger;
    private Timer? _timer;

    public ExpiredFilesCleanupService(ILogger<ExpiredFilesCleanupService> logger, DefaultTusConfiguration config)
    {
        _logger = logger;
        _expirationStore = (ITusExpirationStore)config.Store;
        _expiration = config.Expiration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RunCleanup(cancellationToken);

        async void TimerCallback(object? e) =>
            await RunCleanup((CancellationToken)(e ?? throw new ArgumentNullException(nameof(e))));

        _timer = new Timer(
            TimerCallback,
            cancellationToken,
            TimeSpan.Zero,
            _expiration.Timeout);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private async Task RunCleanup(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Running cleanup job...");
            var numberOfRemovedFiles = await _expirationStore.RemoveExpiredFilesAsync(cancellationToken);

            _logger.LogInformation(
                $"Removed {numberOfRemovedFiles} expired files. Scheduled to run again in {_expiration.Timeout.TotalMilliseconds} ms");
        }
        catch (Exception exc)
        {
            _logger.LogWarning("Failed to run cleanup job: " + exc.Message);
        }
    }
}
