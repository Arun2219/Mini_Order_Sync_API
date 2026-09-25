using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Mini_Order_Sync_API.Configuration;

namespace Mini_Order_Sync_API.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;

    public ApiKeyMiddleware(RequestDelegate _next, IOptions<ApiKeyOptions> options)
    {
        this._next = _next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Allow Swagger and root discovery endpoints without API key
        if (path.StartsWith("/swagger") || path == "/" || path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        // Check if API key header exists
        if (!context.Request.Headers.TryGetValue(_options.HeaderName, out var extractedApiKey))
        {
            await WriteUnauthorizedResponseAsync(context, $"Missing API key in header '{_options.HeaderName}'.");
            return;
        }

        // Validate API key value
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || !string.Equals(_options.ApiKey, extractedApiKey))
        {
            await WriteUnauthorizedResponseAsync(context, "Invalid API key.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.ContentType = "application/json";

        var response = new
        {
            statusCode = (int)HttpStatusCode.Unauthorized,
            error = "Unauthorized",
            message
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}
