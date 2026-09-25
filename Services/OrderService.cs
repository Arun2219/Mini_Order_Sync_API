using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Mini_Order_Sync_API.Data;
using Mini_Order_Sync_API.DTOs;
using Mini_Order_Sync_API.Models;

namespace Mini_Order_Sync_API.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, OrderResponseDto? Order, bool IsDuplicate)> CreateOrderAsync(
        CreateOrderRequestDto request, CancellationToken cancellationToken = default)
    {
        // 1. Validate line items calculation
        decimal calculatedTotal = 0;
        foreach (var line in request.OrderLines)
        {
            if (line.Quantity <= 0)
            {
                return (false, $"Line item SKU '{line.Sku}' must have a quantity greater than 0.", null, false);
            }

            if (line.UnitPrice <= 0)
            {
                return (false, $"Line item SKU '{line.Sku}' must have a unit price greater than 0.", null, false);
            }

            calculatedTotal += line.UnitPrice * line.Quantity;
        }

        if (Math.Round(calculatedTotal, 2) != Math.Round(request.TotalAmount, 2))
        {
            return (false, 
                $"TotalAmount ({request.TotalAmount:F2}) does not match the sum of line items ({calculatedTotal:F2}).", 
                null, 
                false);
        }

        // 2. Proactive duplicate check
        var exists = await _orderRepository.ExistsByOrderNumberAsync(request.OrderNumber, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Order with OrderNumber '{OrderNumber}' already exists.", request.OrderNumber);
            return (false, $"An order with OrderNumber '{request.OrderNumber}' already exists.", null, true);
        }

        // 3. Map DTO to domain model
        var order = new Order
        {
            OrderNumber = request.OrderNumber.Trim(),
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            TotalAmount = Math.Round(request.TotalAmount, 2),
            Status = OrderStatus.Pending,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            OrderLines = request.OrderLines.Select(l => new OrderLine
            {
                Sku = l.Sku.Trim(),
                ProductName = l.ProductName.Trim(),
                Quantity = l.Quantity,
                UnitPrice = Math.Round(l.UnitPrice, 2),
                TotalPrice = Math.Round(l.UnitPrice * l.Quantity, 2)
            }).ToList()
        };

        // 4. Save to database with Dapper
        try
        {
            var orderId = await _orderRepository.CreateOrderAsync(order, cancellationToken);
            order.Id = orderId;

            _logger.LogInformation("Successfully created order '{OrderNumber}' with ID {OrderId} (Status: Pending).", 
                order.OrderNumber, order.Id);

            return (true, null, MapToDto(order), false);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // SQL unique constraint violation (duplicate key race condition)
            _logger.LogWarning(ex, "Unique constraint violation on OrderNumber '{OrderNumber}'.", request.OrderNumber);
            return (false, $"An order with OrderNumber '{request.OrderNumber}' already exists.", null, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order '{OrderNumber}'.", request.OrderNumber);
            return (false, "An error occurred while saving the order.", null, false);
        }
    }

    public async Task<(bool IsValid, string? ErrorMessage, PagedResultDto<OrderResponseDto>? Result)> GetOrdersAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // 1. Validate status filter if supplied
        if (!string.IsNullOrWhiteSpace(status) && !OrderStatus.IsValid(status))
        {
            var allowed = string.Join(", ", OrderStatus.ValidStatuses);
            return (false, $"Invalid status '{status}'. Allowed values are: {allowed}.", null);
        }

        // 2. Validate pagination params
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 100 ? 100 : pageSize);

        var (orders, totalCount) = await _orderRepository.GetPagedOrdersAsync(
            status, page, pageSize, cancellationToken);

        var result = new PagedResultDto<OrderResponseDto>
        {
            Items = orders.Select(MapToDto),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return (true, null, result);
    }

    public async Task<OrderResponseDto?> GetOrderByNumberAsync(
        string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByOrderNumberAsync(orderNumber.Trim(), cancellationToken);
        return order == null ? null : MapToDto(order);
    }

    private static OrderResponseDto MapToDto(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            RetryCount = order.RetryCount,
            ErrorMessage = order.ErrorMessage,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            SyncedAtUtc = order.SyncedAtUtc,
            OrderLines = order.OrderLines.Select(l => new OrderLineResponseDto
            {
                Id = l.Id,
                Sku = l.Sku,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TotalPrice = l.TotalPrice
            }).ToList()
        };
    }
}
