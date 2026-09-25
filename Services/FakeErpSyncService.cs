using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mini_Order_Sync_API.Configuration;
using Mini_Order_Sync_API.Models;

namespace Mini_Order_Sync_API.Services;

public class FakeErpSyncService : IErpSyncService
{
    private readonly ErpSettings _settings;
    private readonly ILogger<FakeErpSyncService> _logger;
    private static readonly Random _random = new();

    public FakeErpSyncService(IOptions<ErpSettings> settings, ILogger<FakeErpSyncService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage)> SyncOrderToErpAsync(
        Order order, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting ERP sync for Order '{OrderNumber}' (Current retry count: {RetryCount})...", 
            order.OrderNumber, order.RetryCount);

        // Simulate network latency (between 100ms and 300ms)
        await Task.Delay(_random.Next(100, 300), cancellationToken);

        // Simulate 20% random failure
        int roll = _random.Next(1, 101); // 1 to 100
        if (roll <= _settings.FailureRatePercentage)
        {
            var simulatedErrors = new[]
            {
                "ERP Gateway Timeout: Remote ERP server failed to respond within 5000ms.",
                "ERP Inventory Lock Error: Target warehouse lock could not be acquired.",
                "ERP 503 Service Unavailable: Remote ERP endpoint undergoing maintenance.",
                "ERP Rate Limit Exceeded: Upstream HTTP 429 Too Many Requests."
            };

            var error = simulatedErrors[_random.Next(simulatedErrors.Length)];
            _logger.LogWarning("ERP sync failed for Order '{OrderNumber}': {ErrorMessage}", order.OrderNumber, error);
            return (false, error);
        }

        _logger.LogInformation("ERP sync successfully completed for Order '{OrderNumber}'.", order.OrderNumber);
        return (true, null);
    }
}
