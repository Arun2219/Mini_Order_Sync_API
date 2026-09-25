-- ============================================================================
-- Script: 01_CreateSchema.sql
-- Description: Creates the Orders and OrderLines tables with unique constraint,
--              foreign key relationship, and appropriate indexes for Dapper.
-- ============================================================================

-- 1. Create Orders Table
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
        
        -- Primary Key
        CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (Id),

        -- Unique Constraint to prevent duplicate order ingestion
        CONSTRAINT UQ_Orders_OrderNumber UNIQUE (OrderNumber)
    );

    -- Index for fast status querying by Background Worker (e.g., finding 'Pending' orders)
    CREATE NONCLUSTERED INDEX IX_Orders_Status ON Orders (Status, RetryCount);
    
    -- Index on OrderNumber for fast lookups
    CREATE NONCLUSTERED INDEX IX_Orders_OrderNumber ON Orders (OrderNumber);
END
GO

-- 2. Create OrderLines Table
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
        
        -- Primary Key
        CONSTRAINT PK_OrderLines PRIMARY KEY CLUSTERED (Id),
        
        -- One-to-Many Foreign Key Constraint linking OrderLine to Order
        CONSTRAINT FK_OrderLines_Orders FOREIGN KEY (OrderId)
            REFERENCES Orders(Id)
            ON DELETE CASCADE
    );

    -- Index for fast retrieval of order lines by OrderId
    CREATE NONCLUSTERED INDEX IX_OrderLines_OrderId ON OrderLines (OrderId);
END
GO
