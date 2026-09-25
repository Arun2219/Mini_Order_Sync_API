using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Mini_Order_Sync_API.Configuration;
using Mini_Order_Sync_API.Models;
using Mini_Order_Sync_API.Services;
using Xunit;

namespace Mini_Order_Sync_API.Tests;

public class FakeErpSyncServiceTests
{
    private readonly Mock<ILogger<FakeErpSyncService>> _loggerMock = new();

    [Fact]
    public async Task SyncOrderToErpAsync_WhenFailureRateIsZero_AlwaysSucceeds()
    {
        // Arrange: 0% failure rate
        var options = Options.Create(new ErpSettings
        {
            FailureRatePercentage = 0,
            MaxRetryAttempts = 3,
            WorkerPollingIntervalSeconds = 10
        });

        var erpService = new FakeErpSyncService(options, _loggerMock.Object);
        var order = new Order { OrderNumber = "ORD-SUCCESS-1", TotalAmount = 50m };

        // Act
        var (success, errorMessage) = await erpService.SyncOrderToErpAsync(order);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task SyncOrderToErpAsync_WhenFailureRateIsHundred_AlwaysFails()
    {
        // Arrange: 100% failure rate
        var options = Options.Create(new ErpSettings
        {
            FailureRatePercentage = 100,
            MaxRetryAttempts = 3,
            WorkerPollingIntervalSeconds = 10
        });

        var erpService = new FakeErpSyncService(options, _loggerMock.Object);
        var order = new Order { OrderNumber = "ORD-FAIL-1", TotalAmount = 50m };

        // Act
        var (success, errorMessage) = await erpService.SyncOrderToErpAsync(order);

        // Assert
        Assert.False(success);
        Assert.NotNull(errorMessage);
    }
}
