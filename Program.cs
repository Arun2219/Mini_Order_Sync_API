using Microsoft.OpenApi.Models;
using Mini_Order_Sync_API.Configuration;
using Mini_Order_Sync_API.Data;
using Mini_Order_Sync_API.Middleware;
using Mini_Order_Sync_API.Services;
using Mini_Order_Sync_API.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Configuration Options
builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.Configure<ErpSettings>(
    builder.Configuration.GetSection(ErpSettings.SectionName));

// 2. Register Database & Repositories (Dapper Data Layer)
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// 3. Register Business & Integration Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IErpSyncService, FakeErpSyncService>();

// 4. Register Background Worker (Hosted Service)
builder.Services.AddHostedService<OrderSyncBackgroundWorker>();

// 5. Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. Configure Swagger/OpenAPI with X-Api-Key Header Authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mini Order Synchronization API",
        Version = "v1",
        Description = "ASP.NET Core .NET 8 Web API with Dapper, SQL Server, and Background Worker for order synchronization."
    });

    // Add API Key definition to Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Enter your API Key into the 'X-Api-Key' header to access protected endpoints. Example: secret-mini-order-sync-api-key-2026",
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                In = ParameterLocation.Header
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Configure HTTP Request Pipeline
if (app.Environment.IsDevelopment() || true) // Enable Swagger in all environments for take-home testing
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mini Order Sync API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI directly at the root URL (http://localhost:5000/)
    });
}

// 8. Custom API Key Security Middleware
app.UseMiddleware<ApiKeyMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();

// 9. Initialize Database (Ensures SQL Server tables exist)
DbInitializer.Initialize(app.Services.GetRequiredService<ISqlConnectionFactory>());

app.Run();

