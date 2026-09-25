using Mini_Order_Sync_API.Models;

namespace Mini_Order_Sync_API.Data;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task<bool> ExistsByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedOrdersAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Order>> GetPendingOrdersForSyncAsync(int maxRetries, int limit = 50, CancellationToken cancellationToken = default);
    Task<bool> UpdateSyncStatusAsync(int id, string status, int retryCount, string? errorMessage, DateTime? syncedAtUtc, CancellationToken cancellationToken = default);
}
