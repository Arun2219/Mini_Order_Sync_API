using Microsoft.AspNetCore.Mvc;
using Mini_Order_Sync_API.DTOs;
using Mini_Order_Sync_API.Services;

namespace Mini_Order_Sync_API.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IOrderService _orderService;

    public WebhooksController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Ingests a new order from an external eCommerce webhook.
    /// Saves the order as 'Pending' in SQL Server for asynchronous background synchronization.
    /// </summary>
    /// <param name="request">The order payload containing customer details and order lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 202 Accepted with the created order metadata, 400 Bad Request, or 409 Conflict.</returns>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestOrder(
        [FromBody] CreateOrderRequestDto request, 
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, errorMessage, order, isDuplicate) = await _orderService.CreateOrderAsync(request, cancellationToken);

        if (isDuplicate)
        {
            return Conflict(new
            {
                statusCode = StatusCodes.Status409Conflict,
                error = "DuplicateOrder",
                message = errorMessage
            });
        }

        if (!success)
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                error = "ValidationError",
                message = errorMessage
            });
        }

        return AcceptedAtAction(
            actionName: nameof(OrdersController.GetOrderByNumber),
            controllerName: "Orders",
            routeValues: new { orderNumber = order!.OrderNumber },
            value: order
        );
    }
}
