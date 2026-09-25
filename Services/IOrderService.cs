using Mini_Order_Sync_API.DTOs;

namespace Mini_Order_Sync_API.Services;

public interface IOrderService
{
    Task<(bool Success, string? ErrorMessage, OrderResponseDto? Order, bool IsDuplicate)> CreateOrderAsync(
        CreateOrderRequestDto request, CancellationToken cancellationToken = default);

    Task<(bool IsValid, string? ErrorMessage, PagedResultDto<OrderResponseDto>? Result)> GetOrdersAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OrderResponseDto?> GetOrderByNumberAsync(
        string orderNumber, CancellationToken cancellationToken = default);
}
