using Microsoft.Extensions.Logging;
using Moq;
using Mini_Order_Sync_API.Data;
using Mini_Order_Sync_API.DTOs;
using Mini_Order_Sync_API.Models;
using Mini_Order_Sync_API.Services;
using Xunit;

namespace Mini_Order_Sync_API.Tests;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<ILogger<OrderService>> _loggerMock;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _loggerMock = new Mock<ILogger<OrderService>>();
        _orderService = new OrderService(_orderRepoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidPayload_ReturnsSuccessAndPendingStatus()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            OrderNumber = "ORD-TEST-001",
            CustomerName = "John Doe",
            CustomerEmail = "john@example.com",
            TotalAmount = 100.00m,
            OrderLines =
            [
                new CreateOrderLineDto
                {
                    Sku = "SKU-1",
                    ProductName = "Item 1",
                    Quantity = 2,
                    UnitPrice = 50.00m
                }
            ]
        };

        _orderRepoMock.Setup(r => r.ExistsByOrderNumberAsync(request.OrderNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _orderRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var (success, errorMessage, order, isDuplicate) = await _orderService.CreateOrderAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.False(isDuplicate);
        Assert.NotNull(order);
        Assert.Equal("ORD-TEST-001", order.OrderNumber);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(100.00m, order.TotalAmount);
        Assert.Single(order.OrderLines);
    }

    [Fact]
    public async Task CreateOrderAsync_TotalAmountMismatch_ReturnsValidationError()
    {
        // Arrange: Sum of lines is 50.00, but TotalAmount is declared as 100.00
        var request = new CreateOrderRequestDto
        {
            OrderNumber = "ORD-MISMATCH-001",
            CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com",
            TotalAmount = 100.00m,
            OrderLines =
            [
                new CreateOrderLineDto
                {
                    Sku = "SKU-1",
                    ProductName = "Item 1",
                    Quantity = 1,
                    UnitPrice = 50.00m
                }
            ]
        };

        // Act
        var (success, errorMessage, order, isDuplicate) = await _orderService.CreateOrderAsync(request);

        // Assert
        Assert.False(success);
        Assert.False(isDuplicate);
        Assert.Null(order);
        Assert.Contains("does not match the sum of line items", errorMessage);
    }

    [Fact]
    public async Task CreateOrderAsync_DuplicateOrderNumber_ReturnsDuplicateConflict()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            OrderNumber = "ORD-DUPLICATE",
            CustomerName = "Alice",
            CustomerEmail = "alice@example.com",
            TotalAmount = 25.00m,
            OrderLines =
            [
                new CreateOrderLineDto
                {
                    Sku = "SKU-1",
                    ProductName = "Item 1",
                    Quantity = 1,
                    UnitPrice = 25.00m
                }
            ]
        };

        _orderRepoMock.Setup(r => r.ExistsByOrderNumberAsync(request.OrderNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Order already exists

        // Act
        var (success, errorMessage, order, isDuplicate) = await _orderService.CreateOrderAsync(request);

        // Assert
        Assert.False(success);
        Assert.True(isDuplicate);
        Assert.Null(order);
        Assert.Contains("already exists", errorMessage);
    }

    [Fact]
    public async Task GetOrdersAsync_InvalidStatusFilter_ReturnsValidationError()
    {
        // Act
        var (isValid, errorMessage, result) = await _orderService.GetOrdersAsync("InvalidStatus", 1, 10);

        // Assert
        Assert.False(isValid);
        Assert.Null(result);
        Assert.Contains("Invalid status 'InvalidStatus'", errorMessage);
    }

    [Fact]
    public async Task GetOrdersAsync_ValidQuery_ReturnsPaginatedOrders()
    {
        // Arrange
        var sampleOrders = new List<Order>
        {
            new() { Id = 1, OrderNumber = "ORD-001", CustomerName = "User 1", TotalAmount = 100m, Status = OrderStatus.Pending },
            new() { Id = 2, OrderNumber = "ORD-002", CustomerName = "User 2", TotalAmount = 200m, Status = OrderStatus.Pending }
        };

        _orderRepoMock.Setup(r => r.GetPagedOrdersAsync(OrderStatus.Pending, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((sampleOrders, 2));

        // Act
        var (isValid, errorMessage, result) = await _orderService.GetOrdersAsync(OrderStatus.Pending, 1, 10);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetOrderByNumberAsync_ExistingOrder_ReturnsOrderWithLines()
    {
        // Arrange
        var sampleOrder = new Order
        {
            Id = 1,
            OrderNumber = "ORD-12345",
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            TotalAmount = 75.00m,
            Status = OrderStatus.Synced,
            OrderLines =
            [
                new OrderLine { Id = 10, OrderId = 1, Sku = "SKU-99", ProductName = "Product 99", Quantity = 3, UnitPrice = 25.00m, TotalPrice = 75.00m }
            ]
        };

        _orderRepoMock.Setup(r => r.GetByOrderNumberAsync("ORD-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleOrder);

        // Act
        var result = await _orderService.GetOrderByNumberAsync("ORD-12345");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ORD-12345", result.OrderNumber);
        Assert.Equal(OrderStatus.Synced, result.Status);
        Assert.Single(result.OrderLines);
        Assert.Equal("SKU-99", result.OrderLines[0].Sku);
    }
}
