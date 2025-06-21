using System.Net;
using System.Net.Sockets;

namespace TicketManagement.Web.Client.Services;

public static class RetryHelper
{
    public static bool ShouldRetry(HttpStatusCode statusCode, string? errorMessage = null)
    {
        return statusCode switch
        {
            // Network/temporary errors - should retry
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            
            // Client errors - should NOT retry
            HttpStatusCode.BadRequest => false,
            HttpStatusCode.Unauthorized => false,
            HttpStatusCode.Forbidden => false,
            HttpStatusCode.NotFound => false,
            HttpStatusCode.MethodNotAllowed => false,
            HttpStatusCode.Conflict => false,
            HttpStatusCode.UnprocessableEntity => false,
            
            // Default for unknown status codes
            _ => (int)statusCode >= 500
        };
    }

    public static bool ShouldRetry(Exception exception)
    {
        return exception switch
        {
            // Network errors - should retry
            HttpRequestException => true,
            TaskCanceledException => true,
            SocketException => true,
            
            // Application errors - should NOT retry
            ArgumentNullException => false,
            ArgumentException => false,
            UnauthorizedAccessException => false,
            InvalidOperationException => false,
            
            // Default for unknown exceptions
            _ => false
        };
    }

    public static TimeSpan CalculateDelay(int attemptNumber, TimeSpan baseDelay)
    {
        // Exponential backoff with jitter
        var delay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        
        // Add random jitter (±25%)
        var jitter = Random.Shared.NextDouble() * 0.5 - 0.25;
        var jitteredDelay = delay.TotalMilliseconds * (1 + jitter);
        
        return TimeSpan.FromMilliseconds(Math.Max(100, jitteredDelay));
    }
}