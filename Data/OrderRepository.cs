using System.Data;
using Dapper;
using Mini_Order_Sync_API.Models;

namespace Mini_Order_Sync_API.Data;

public class OrderRepository : IOrderRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OrderRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            const string insertOrderSql = @"
                INSERT INTO Orders (
                    OrderNumber, CustomerName, CustomerEmail, TotalAmount, 
                    Status, RetryCount, ErrorMessage, CreatedAtUtc, UpdatedAtUtc, SyncedAtUtc
                )
                VALUES (
                    @OrderNumber, @CustomerName, @CustomerEmail, @TotalAmount, 
                    @Status, @RetryCount, @ErrorMessage, @CreatedAtUtc, @UpdatedAtUtc, @SyncedAtUtc
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var commandDefinition = new CommandDefinition(
                insertOrderSql,
                new
                {
                    order.OrderNumber,
                    order.CustomerName,
                    order.CustomerEmail,
                    order.TotalAmount,
                    order.Status,
                    order.RetryCount,
                    order.ErrorMessage,
                    order.CreatedAtUtc,
                    order.UpdatedAtUtc,
                    order.SyncedAtUtc
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            );

            var orderId = Convert.ToInt32(await connection.ExecuteScalarAsync(commandDefinition));
            order.Id = orderId;

            if (order.OrderLines.Count > 0)
            {
                const string insertLineSql = @"
                    INSERT INTO OrderLines (
                        OrderId, Sku, ProductName, Quantity, UnitPrice, TotalPrice
                    )
                    VALUES (
                        @OrderId, @Sku, @ProductName, @Quantity, @UnitPrice, @TotalPrice
                    );";

                var lineParams = order.OrderLines.Select(line => new
                {
                    OrderId = orderId,
                    line.Sku,
                    line.ProductName,
                    line.Quantity,
                    line.UnitPrice,
                    line.TotalPrice
                });

                var linesCommand = new CommandDefinition(
                    insertLineSql,
                    lineParams,
                    transaction: transaction,
                    cancellationToken: cancellationToken
                );

                await connection.ExecuteAsync(linesCommand);
            }

            transaction.Commit();
            return orderId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> ExistsByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Orders WHERE OrderNumber = @OrderNumber
            ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(sql, new { OrderNumber = orderNumber }, cancellationToken: cancellationToken);
        var result = await connection.ExecuteScalarAsync(command);
        return Convert.ToInt32(result) == 1;
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.TotalAmount, 
                o.Status, o.RetryCount, o.ErrorMessage, o.CreatedAtUtc, o.UpdatedAtUtc, o.SyncedAtUtc,
                l.Id, l.OrderId, l.Sku, l.ProductName, l.Quantity, l.UnitPrice, l.TotalPrice
            FROM Orders o
            LEFT JOIN OrderLines l ON o.Id = l.OrderId
            WHERE o.OrderNumber = @OrderNumber;";

        var orderDictionary = new Dictionary<int, Order>();

        var command = new CommandDefinition(
            sql,
            new { OrderNumber = orderNumber },
            cancellationToken: cancellationToken
        );

        await connection.QueryAsync<Order, OrderLine, Order>(
            command,
            (order, orderLine) =>
            {
                if (!orderDictionary.TryGetValue(order.Id, out var currentOrder))
                {
                    currentOrder = order;
                    currentOrder.OrderLines = [];
                    orderDictionary.Add(currentOrder.Id, currentOrder);
                }

                if (orderLine != null && orderLine.Id > 0)
                {
                    currentOrder.OrderLines.Add(orderLine);
                }

                return currentOrder;
            },
            splitOn: "Id"
        );

        return orderDictionary.Values.FirstOrDefault();
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.TotalAmount, 
                o.Status, o.RetryCount, o.ErrorMessage, o.CreatedAtUtc, o.UpdatedAtUtc, o.SyncedAtUtc,
                l.Id, l.OrderId, l.Sku, l.ProductName, l.Quantity, l.UnitPrice, l.TotalPrice
            FROM Orders o
            LEFT JOIN OrderLines l ON o.Id = l.OrderId
            WHERE o.Id = @Id;";

        var orderDictionary = new Dictionary<int, Order>();

        var command = new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken
        );

        await connection.QueryAsync<Order, OrderLine, Order>(
            command,
            (order, orderLine) =>
            {
                if (!orderDictionary.TryGetValue(order.Id, out var currentOrder))
                {
                    currentOrder = order;
                    currentOrder.OrderLines = [];
                    orderDictionary.Add(currentOrder.Id, currentOrder);
                }

                if (orderLine != null && orderLine.Id > 0)
                {
                    currentOrder.OrderLines.Add(orderLine);
                }

                return currentOrder;
            },
            splitOn: "Id"
        );

        return orderDictionary.Values.FirstOrDefault();
    }

    public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedOrdersAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        const string countSql = @"
            SELECT COUNT(1) 
            FROM Orders 
            WHERE (@Status IS NULL OR Status = @Status);";

        const string itemsSql = @"
            SELECT Id, OrderNumber, CustomerName, CustomerEmail, TotalAmount, 
                   Status, RetryCount, ErrorMessage, CreatedAtUtc, UpdatedAtUtc, SyncedAtUtc
            FROM Orders
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAtUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var countCommand = new CommandDefinition(
            countSql, 
            new { Status = status }, 
            cancellationToken: cancellationToken
        );
        var totalCount = Convert.ToInt32(await connection.ExecuteScalarAsync(countCommand));

        var itemsCommand = new CommandDefinition(
            itemsSql, 
            new { Status = status, Offset = offset, PageSize = pageSize }, 
            cancellationToken: cancellationToken
        );
        var orders = await connection.QueryAsync<Order>(itemsCommand);

        return (orders, totalCount);
    }

    public async Task<IEnumerable<Order>> GetPendingOrdersForSyncAsync(
        int maxRetries, int limit = 50, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT TOP (@Limit)
                o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.TotalAmount, 
                o.Status, o.RetryCount, o.ErrorMessage, o.CreatedAtUtc, o.UpdatedAtUtc, o.SyncedAtUtc,
                l.Id, l.OrderId, l.Sku, l.ProductName, l.Quantity, l.UnitPrice, l.TotalPrice
            FROM Orders o
            LEFT JOIN OrderLines l ON o.Id = l.OrderId
            WHERE o.Status = 'Pending' AND o.RetryCount < @MaxRetries
            ORDER BY o.CreatedAtUtc ASC;";

        var orderDictionary = new Dictionary<int, Order>();

        var command = new CommandDefinition(
            sql,
            new { MaxRetries = maxRetries, Limit = limit },
            cancellationToken: cancellationToken
        );

        await connection.QueryAsync<Order, OrderLine, Order>(
            command,
            (order, orderLine) =>
            {
                if (!orderDictionary.TryGetValue(order.Id, out var currentOrder))
                {
                    currentOrder = order;
                    currentOrder.OrderLines = [];
                    orderDictionary.Add(currentOrder.Id, currentOrder);
                }

                if (orderLine != null && orderLine.Id > 0)
                {
                    currentOrder.OrderLines.Add(orderLine);
                }

                return currentOrder;
            },
            splitOn: "Id"
        );

        return orderDictionary.Values;
    }

    public async Task<bool> UpdateSyncStatusAsync(
        int id, string status, int retryCount, string? errorMessage, DateTime? syncedAtUtc, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE Orders
            SET Status = @Status,
                RetryCount = @RetryCount,
                ErrorMessage = @ErrorMessage,
                SyncedAtUtc = @SyncedAtUtc,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id;";

        var command = new CommandDefinition(
            sql,
            new
            {
                Id = id,
                Status = status,
                RetryCount = retryCount,
                ErrorMessage = errorMessage,
                SyncedAtUtc = syncedAtUtc
            },
            cancellationToken: cancellationToken
        );

        var rowsAffected = await connection.ExecuteAsync(command);
        return rowsAffected > 0;
    }
}
