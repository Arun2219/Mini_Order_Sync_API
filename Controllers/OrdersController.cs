using Microsoft.AspNetCore.Mvc;
using Mini_Order_Sync_API.DTOs;
using Mini_Order_Sync_API.Services;

namespace Mini_Order_Sync_API.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Retrieves a paginated list of orders, optionally filtered by status (Pending, Synced, Failed).
    /// </summary>
    /// <param name="status">Optional status filter: 'Pending', 'Synced', 'Failed'.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 10, max: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated orders list.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<OrderResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (isValid, errorMessage, result) = await _orderService.GetOrdersAsync(status, page, pageSize, cancellationToken);

        if (!isValid)
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                error = "InvalidFilter",
                message = errorMessage
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves full order details including line items, customer info, and sync status by OrderNumber.
    /// </summary>
    /// <param name="orderNumber">Unique order number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full order details or 404 Not Found.</returns>
    [HttpGet("{orderNumber}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderByNumber(
        [FromRoute] string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(new { message = "OrderNumber is required." });
        }

        var order = await _orderService.GetOrderByNumberAsync(orderNumber, cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                statusCode = StatusCodes.Status404NotFound,
                error = "NotFound",
                message = $"Order with OrderNumber '{orderNumber}' was not found."
            });
        }

        return Ok(order);
    }
}
