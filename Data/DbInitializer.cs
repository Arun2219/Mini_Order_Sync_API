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

        const string sqlServerSchema = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
            BEGIN
                CREATE TABLE Orders (
                    Id INT IDENTITY(1,1) NOT NULL,
                    OrderNumber NVARCHAR(50) NOT NULL,
                    CustomerName NVARCHAR(100) NOT NULL,
                    CustomerEmail NVARCHAR(255) NOT NULL,
                    TotalAmount DECIMAL(18, 2) NOT NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    RetryCount INT NOT NULL DEFAULT 0,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    SyncedAtUtc DATETIME2 NULL,
                    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT UQ_Orders_OrderNumber UNIQUE (OrderNumber)
                );

                CREATE NONCLUSTERED INDEX IX_Orders_Status ON Orders (Status, RetryCount);
                CREATE NONCLUSTERED INDEX IX_Orders_OrderNumber ON Orders (OrderNumber);
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderLines')
            BEGIN
                CREATE TABLE OrderLines (
                    Id INT IDENTITY(1,1) NOT NULL,
                    OrderId INT NOT NULL,
                    Sku NVARCHAR(50) NOT NULL,
                    ProductName NVARCHAR(200) NOT NULL,
                    Quantity INT NOT NULL,
                    UnitPrice DECIMAL(18, 2) NOT NULL,
                    TotalPrice DECIMAL(18, 2) NOT NULL,
                    CONSTRAINT PK_OrderLines PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_OrderLines_Orders FOREIGN KEY (OrderId)
                        REFERENCES Orders(Id) ON DELETE CASCADE
                );

                CREATE NONCLUSTERED INDEX IX_OrderLines_OrderId ON OrderLines (OrderId);
            END;";

        connection.Execute(sqlServerSchema);
    }
}
