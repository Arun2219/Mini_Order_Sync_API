using System.Data;
using Dapper;

namespace Mini_Order_Sync_API.Data;

public static class DbInitializer
{
    public static void Initialize(ISqlConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
        {
            const string sqliteSchema = @"
                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderNumber TEXT NOT NULL UNIQUE,
                    CustomerName TEXT NOT NULL,
                    CustomerEmail TEXT NOT NULL,
                    TotalAmount REAL NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Pending',
                    RetryCount INTEGER NOT NULL DEFAULT 0,
                    ErrorMessage TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    SyncedAtUtc TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS OrderLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,
                    Sku TEXT NOT NULL,
                    ProductName TEXT NOT NULL,
                    Quantity INTEGER NOT NULL,
                    UnitPrice REAL NOT NULL,
                    TotalPrice REAL NOT NULL,
                    FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS IX_Orders_Status ON Orders (Status, RetryCount);
                CREATE INDEX IF NOT EXISTS IX_Orders_OrderNumber ON Orders (OrderNumber);
                CREATE INDEX IF NOT EXISTS IX_OrderLines_OrderId ON OrderLines (OrderId);
            ";

            connection.Execute(sqliteSchema);
        }
    }
}
