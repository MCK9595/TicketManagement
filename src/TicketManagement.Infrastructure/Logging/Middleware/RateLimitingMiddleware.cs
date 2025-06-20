using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;

namespace TicketManagement.Infrastructure.Logging.Middleware;

/// <summary>
/// Simple rate limiting middleware
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, ClientRequestInfo> _clients = new();
    
    // Configuration
    private readonly int _maxRequests = 100; // Max requests per window
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1); // Time window

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip health check and auth endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path?.Contains("/health") == true || 
            path?.Contains("/auth") == true ||
            path?.Contains("/swagger") == true)
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var now = DateTime.UtcNow;
        
        var client = _clients.AddOrUpdate(clientId, 
            new ClientRequestInfo { LastRequest = now, RequestCount = 1 },
            (key, existing) =>
            {
                // Reset counter if outside time window
                if (now - existing.LastRequest > _timeWindow)
                {
                    existing.RequestCount = 1;
                    existing.LastRequest = now;
                }
                else
                {
                    existing.RequestCount++;
                    existing.LastRequest = now;
                }
                return existing;
            });

        if (client.RequestCount > _maxRequests)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId}. Requests: {RequestCount}", 
                clientId, client.RequestCount);
                
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _timeWindow.TotalSeconds.ToString();
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID first, then fall back to IP
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private class ClientRequestInfo
    {
        public DateTime LastRequest { get; set; }
        public int RequestCount { get; set; }
    }
}