using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mini_Order_Sync_API.Configuration;
using Mini_Order_Sync_API.Data;
using Mini_Order_Sync_API.Models;
using Mini_Order_Sync_API.Services;

namespace Mini_Order_Sync_API.Workers;

public class OrderSyncBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ErpSettings _settings;
    private readonly ILogger<OrderSyncBackgroundWorker> _logger;

    public OrderSyncBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ErpSettings> settings,
        ILogger<OrderSyncBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderSyncBackgroundWorker started. Polling every {Interval} seconds (Max Retries: {MaxRetries}, Simulated Failure Rate: {FailRate}%).",
            _settings.WorkerPollingIntervalSeconds, _settings.MaxRetryAttempts, _settings.FailureRatePercentage);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during background order synchronization cycle.");
            }

            // Wait for the configured polling interval
            await Task.Delay(TimeSpan.FromSeconds(_settings.WorkerPollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("OrderSyncBackgroundWorker is stopping.");
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        // BackgroundService is a Singleton, so we create a scope to resolve Scoped dependencies (Repository & Services)
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var erpService = scope.ServiceProvider.GetRequiredService<IErpSyncService>();

        var pendingOrders = (await repository.GetPendingOrdersForSyncAsync(_settings.MaxRetryAttempts, 50, cancellationToken)).ToList();

        if (pendingOrders.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} pending order(s) to synchronize with ERP.", pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var (success, errorMessage) = await erpService.SyncOrderToErpAsync(order, cancellationToken);

                if (success)
                {
                    await repository.UpdateSyncStatusAsync(
                        id: order.Id,
                        status: OrderStatus.Synced,
                        retryCount: order.RetryCount,
                        errorMessage: null,
                        syncedAtUtc: DateTime.UtcNow,
                        cancellationToken: cancellationToken
                    );

                    _logger.LogInformation("Successfully synced Order '{OrderNumber}' to ERP (Status: Synced).", order.OrderNumber);
                }
                else
                {
                    var newRetryCount = order.RetryCount + 1;
                    var finalStatus = newRetryCount >= _settings.MaxRetryAttempts 
                        ? OrderStatus.Failed 
                        : OrderStatus.Pending;

                    await repository.UpdateSyncStatusAsync(
                        id: order.Id,
                        status: finalStatus,
                        retryCount: newRetryCount,
                        errorMessage: $"Attempt {newRetryCount}/{_settings.MaxRetryAttempts} failed: {errorMessage}",
                        syncedAtUtc: null,
                        cancellationToken: cancellationToken
                    );

                    if (finalStatus == OrderStatus.Failed)
                    {
                        _logger.LogError("Order '{OrderNumber}' reached maximum retry attempts ({MaxRetries}). Marked as FAILED.",
                            order.OrderNumber, _settings.MaxRetryAttempts);
                    }
                    else
                    {
                        _logger.LogWarning("Order '{OrderNumber}' sync attempt failed ({Attempt}/{MaxRetries}). Remaining as PENDING for next cycle.",
                            order.OrderNumber, newRetryCount, _settings.MaxRetryAttempts);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync for Order '{OrderNumber}'.", order.OrderNumber);
            }
        }
    }
}
