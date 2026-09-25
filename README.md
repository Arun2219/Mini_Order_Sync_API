# Mini Order Synchronization Web API

A high-performance, asynchronous Order Synchronization Backend Web API built with **.NET 8**, **ASP.NET Core**, **C#**, **Dapper**, and **Microsoft SQL Server**.

This solution ingests order webhooks from external eCommerce platforms, stores them immediately in SQL Server, and utilizes a resilient **Background Hosted Service** to synchronize pending orders to an external ERP system with automated retry policies and failure handling.

---

## 🏗 Project Architecture

The project follows a clean, decoupled, layered architecture:

```text
Mini_Order_Sync_API/
├── Controllers/            # API Endpoints (Routing, HTTP status codes, Swagger metadata)
│   ├── OrdersController.cs
│   └── WebhooksController.cs
├── Services/               # Business Logic, Validation & ERP Integration Client
│   ├── IOrderService.cs
│   ├── OrderService.cs
│   ├── IErpSyncService.cs
│   └── FakeErpSyncService.cs
├── Data/                   # Database connection factory & Dapper repositories/scripts
│   ├── ISqlConnectionFactory.cs
│   ├── SqlConnectionFactory.cs
│   ├── IOrderRepository.cs
│   ├── OrderRepository.cs
│   └── Scripts/
│       └── 01_CreateSchema.sql
├── Models/                 # Database Domain Entities (mapping SQL tables)
│   ├── Order.cs
│   ├── OrderLine.cs
│   └── OrderStatus.cs
├── DTOs/                   # Data Transfer Objects (Requests, Responses & Pagination)
│   ├── CreateOrderRequestDto.cs
│   ├── OrderResponseDto.cs
│   └── PagedResultDto.cs
├── Workers/                # Background Hosted Services
│   └── OrderSyncBackgroundWorker.cs
├── Configuration/          # Strongly-typed Options models
│   ├── ApiKeyOptions.cs
│   └── ErpSettings.cs
├── Middleware/             # Security / API Key Authentication
│   └── ApiKeyMiddleware.cs
├── tests/                  # xUnit Unit Testing Project
│   ├── OrderServiceTests.cs
│   └── FakeErpSyncServiceTests.cs
├── Program.cs              # Application startup, DI registration, pipeline configuration
└── appsettings.json        # Database connection strings and application configs
```

### Why Each Layer Exists

- **`Controllers/`**: Manages HTTP protocol interactions, routes requests, extracts query/route parameters, and returns standard HTTP status codes (`202 Accepted`, `200 OK`, `400 Bad Request`, `401 Unauthorized`, `404 Not Found`, `409 Conflict`).
- **`Services/`**: Encapsulates business logic, validates that line item totals equal `TotalAmount`, enforces status filtering rules, and communicates with the ERP client.
- **`Data/`**: Manages low-level database operations using **Dapper**. Executes raw parameterized SQL, handles database transactions, and maps SQL result sets to C# domain models.
- **`Models/` & `DTOs/`**: Keeps internal database entities strictly separated from public API contract models.
- **`Workers/`**: Runs independently of HTTP requests as an `IHostedService` / `BackgroundService` to process pending order synchronization cycles.
- **`Middleware/`**: Intercepts HTTP requests to enforce API Key authentication before reaching controllers.

---

## 🚀 Technologies Used

- **.NET 8 (LTS)**
- **ASP.NET Core Web API**
- **C# 12**
- **Dapper (Micro-ORM)** (No EF Core)
- **Microsoft.Data.SqlClient** (ADO.NET provider)
- **Microsoft SQL Server**
- **IHostedService / BackgroundService** (Background Worker)
- **Swashbuckle.AspNetCore** (Swagger / OpenAPI)
- **xUnit & Moq** (Unit Testing)

---

## 📋 Prerequisites

Before running the application, ensure you have:
1. **.NET 8 SDK** ([Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0))
2. **Microsoft SQL Server** (LocalDB, SQL Express, Developer Edition, or Docker SQL Server)
3. **SQL Server Management Studio (SSMS)**, **Azure Data Studio**, or **sqlcmd**

---

## 🗄️ Database Setup & Configuration

### 1. Execute SQL Schema Script
Open SSMS or Azure Data Studio, connect to your SQL Server instance, and run the following commands:

```sql
-- Create database
CREATE DATABASE OrderSyncDb;
GO

USE OrderSyncDb;
GO

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

    -- Index for fast status querying by Background Worker
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
```

*(This script is also located at [Data/Scripts/01_CreateSchema.sql](Data/Scripts/01_CreateSchema.sql)).*

### 2. Configure Connection String
Update `appsettings.json` with your SQL Server connection details:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=OrderSyncDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
  }
}
```

---

## 🔑 API Key Security Configuration

Endpoints are protected with API Key authentication via the `X-Api-Key` request header.

The API key is configured in `appsettings.json`:
```json
{
  "ApiKeySettings": {
    "HeaderName": "X-Api-Key",
    "ApiKey": "secret-mini-order-sync-api-key-2026"
  }
}
```

---

## ▶️ Running the Application

1. Open your terminal in the project root directory:
   ```bash
   dotnet restore
   dotnet run
   ```

2. Access the interactive **Swagger UI** in your browser at:
   ```text
   http://localhost:5000/
   ```

3. To authenticate in Swagger:
   - Click the **Authorize 🔓** button at the top right.
   - Enter `secret-mini-order-sync-api-key-2026`.
   - Click **Authorize**, then **Close**.

---

## 📡 API Endpoints & Sample Requests

### 1. Ingest Webhook Order
**`POST /webhooks/orders`**  
*Ingests an order, validates payload and line item sums, saves to database with status `Pending`, and returns `202 Accepted`.*

**Request:**
```bash
curl -X POST "http://localhost:5000/webhooks/orders" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: secret-mini-order-sync-api-key-2026" \
  -d '{
    "orderNumber": "ORD-2026-001",
    "customerName": "John Doe",
    "customerEmail": "john.doe@example.com",
    "totalAmount": 199.98,
    "orderLines": [
      {
        "sku": "KB-RGB-01",
        "productName": "Mechanical Gaming Keyboard",
        "quantity": 1,
        "unitPrice": 129.99
      },
      {
        "sku": "MS-WL-02",
        "productName": "Wireless Ergonomic Mouse",
        "quantity": 1,
        "unitPrice": 69.99
      }
    ]
  }'
```

**Response (`202 Accepted`):**
```json
{
  "id": 1,
  "orderNumber": "ORD-2026-001",
  "customerName": "John Doe",
  "customerEmail": "john.doe@example.com",
  "totalAmount": 199.98,
  "status": "Pending",
  "retryCount": 0,
  "errorMessage": null,
  "createdAtUtc": "2026-09-24T14:30:00Z",
  "updatedAtUtc": "2026-09-24T14:30:00Z",
  "syncedAtUtc": null,
  "orderLines": [
    {
      "id": 1,
      "sku": "KB-RGB-01",
      "productName": "Mechanical Gaming Keyboard",
      "quantity": 1,
      "unitPrice": 129.99,
      "totalPrice": 129.99
    },
    {
      "id": 2,
      "sku": "MS-WL-02",
      "productName": "Wireless Ergonomic Mouse",
      "quantity": 1,
      "unitPrice": 69.99,
      "totalPrice": 69.99
    }
  ]
}
```

---

### 2. Query Orders with Filtering & Pagination
**`GET /orders?status=Pending&page=1&pageSize=10`**  
*Retrieves paginated orders filtered by status (`Pending`, `Synced`, or `Failed`).*

**Request:**
```bash
curl -X GET "http://localhost:5000/orders?status=Pending&page=1&pageSize=10" \
  -H "X-Api-Key: secret-mini-order-sync-api-key-2026"
```

**Response (`200 OK`):**
```json
{
  "items": [
    {
      "id": 1,
      "orderNumber": "ORD-2026-001",
      "customerName": "John Doe",
      "customerEmail": "john.doe@example.com",
      "totalAmount": 199.98,
      "status": "Pending",
      "retryCount": 0,
      "errorMessage": null,
      "createdAtUtc": "2026-09-24T14:30:00Z",
      "updatedAtUtc": "2026-09-24T14:30:00Z",
      "syncedAtUtc": null,
      "orderLines": []
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

### 3. Get Order by Order Number
**`GET /orders/{orderNumber}`**  
*Retrieves full order information, customer details, synchronization status, and order lines.*

**Request:**
```bash
curl -X GET "http://localhost:5000/orders/ORD-2026-001" \
  -H "X-Api-Key: secret-mini-order-sync-api-key-2026"
```

**Response (`200 OK`):**
```json
{
  "id": 1,
  "orderNumber": "ORD-2026-001",
  "customerName": "John Doe",
  "customerEmail": "john.doe@example.com",
  "totalAmount": 199.98,
  "status": "Synced",
  "retryCount": 0,
  "errorMessage": null,
  "createdAtUtc": "2026-09-24T14:30:00Z",
  "updatedAtUtc": "2026-09-24T14:30:12Z",
  "syncedAtUtc": "2026-09-24T14:30:12Z",
  "orderLines": [
    {
      "id": 1,
      "sku": "KB-RGB-01",
      "productName": "Mechanical Gaming Keyboard",
      "quantity": 1,
      "unitPrice": 129.99,
      "totalPrice": 129.99
    },
    {
      "id": 2,
      "sku": "MS-WL-02",
      "productName": "Wireless Ergonomic Mouse",
      "quantity": 1,
      "unitPrice": 69.99,
      "totalPrice": 69.99
    }
  ]
}
```

---

## ⚙️ Background Worker & Retry Mechanism

The `OrderSyncBackgroundWorker` runs as a hosted service on a background thread:

1. **Polling Interval**: Runs every 10 seconds (configured via `ErpSettings:WorkerPollingIntervalSeconds`).
2. **Batch Retrieval**: Queries up to 50 orders where `Status = 'Pending'` and `RetryCount < MaxRetryAttempts`.
3. **ERP Simulation**: Calls `FakeErpSyncService`, which simulates network latency and a **20% random failure rate**.
4. **State Transition & Retries**:
   - **Success**: Status transitions to `Synced`, `SyncedAtUtc` is timestamped, and `ErrorMessage` is cleared.
   - **Failure**: `RetryCount` is incremented.
     - If `RetryCount < 3`: Status remains `Pending` so the next polling cycle will retry the sync.
     - If `RetryCount >= 3`: Status transitions to `Failed` and the detailed error message is logged to the database.

---

## 🧪 Running Unit Tests

Execute the xUnit test suite from the repository root:

```bash
dotnet test
```

Test coverage includes:
- Order payload validation & calculations
- Duplicate order number detection & rejection
- Status filter validation & pagination bounds
- Dapper domain model to DTO mapping
- Fake ERP random failure simulation & retry boundaries

---

## 💡 Assumptions & Future Improvements

### Assumptions Made:
1. `OrderNumber` is the unique external business key generated by the eCommerce platform.
2. The mock ERP system receives full order details (header and lines).
3. The background worker polls the SQL Server database directly; in a high-scale multi-instance environment, a distributed lock or message queue could be added.

### Future Improvements:
1. **Exponential Backoff**: Implement increasing delay intervals between retries (e.g. 10s $\rightarrow$ 30s $\rightarrow$ 60s) using Polly or SQL Server timestamp filters.
2. **Dead-Letter / Manual Retry Endpoint**: Add a `POST /orders/{orderNumber}/retry` endpoint to manually re-queue failed orders.
3. **Structured Health Checks**: Add ASP.NET Core Health Checks for SQL Server readiness (`/healthz`).
