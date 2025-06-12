namespace TicketManagement.ApiService.Middleware;

/// <summary>
/// セキュリティヘッダーを追加するミドルウェア
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        
        // HSTS (only for HTTPS)
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        // CSP for API endpoints
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers.Add("Content-Security-Policy", "default-src 'none'");
        }

        await _next(context);
    }
}

/// <summary>
/// リクエストログ記録ミドルウェア
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        
        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - start;
            
            _logger.LogInformation(
                "Request {Method} {Path} completed with status {StatusCode} in {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        }
    }
}

/// <summary>
/// レート制限ミドルウェア（簡易版）
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Dictionary<string, List<DateTime>> _requestHistory = new();
    private static readonly object _lock = new();
    private readonly int _maxRequests = 100; // 5分間に100リクエスト
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(5);

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            
            if (!_requestHistory.ContainsKey(clientId))
            {
                _requestHistory[clientId] = new List<DateTime>();
            }

            var clientRequests = _requestHistory[clientId];
            
            // 期限切れのリクエストを削除
            clientRequests.RemoveAll(r => now - r > _timeWindow);
            
            if (clientRequests.Count >= _maxRequests)
            {
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.Headers.Add("Retry-After", "300"); // 5分後に再試行
                return;
            }
            
            clientRequests.Add(now);
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // ユーザーIDがある場合はそれを使用、なければIPアドレスを使用
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
}