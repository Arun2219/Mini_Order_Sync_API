using Mini_Order_Sync_API.Models;

namespace Mini_Order_Sync_API.Services;

public interface IErpSyncService
{
    Task<(bool Success, string? ErrorMessage)> SyncOrderToErpAsync(
        Order order, CancellationToken cancellationToken = default);
}
