using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using TicketManagement.Infrastructure.Logging.Services;

namespace TicketManagement.Infrastructure.Logging.Middleware;

/// <summary>
/// 強化されたログミドルウェア
/// </summary>
public class EnhancedLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnhancedLoggingMiddleware> _logger;
    private readonly IPerformanceLogService _performanceLogService;
    private readonly ISecurityLogService _securityLogService;

    public EnhancedLoggingMiddleware(
        RequestDelegate next,
        ILogger<EnhancedLoggingMiddleware> logger,
        IPerformanceLogService performanceLogService,
        ISecurityLogService securityLogService)
    {
        _next = next;
        _logger = logger;
        _performanceLogService = performanceLogService;
        _securityLogService = securityLogService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        
        // リクエスト情報を記録
        await LogRequestStartAsync(context, requestId);
        
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
            
            stopwatch.Stop();
            
            // レスポンス情報を記録
            await LogRequestCompletedAsync(context, requestId, stopwatch.ElapsedMilliseconds);
            
            // パフォーマンスログ
            await _performanceLogService.LogApiRequestPerformanceAsync(
                context.Request.Path.Value ?? "unknown",
                context.Request.Method,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // エラーログ
            await LogRequestErrorAsync(context, requestId, ex, stopwatch.ElapsedMilliseconds);
            
            throw; // 例外を再スロー
        }
        finally
        {
            // レスポンスボディを元のストリームにコピー
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestStartAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        
        // セキュリティチェック
        await CheckForSuspiciousActivityAsync(context);
        
        // リクエスト開始ログ
        _logger.LogInformation(
            "HTTP Request Start: {Method} {Path} {QueryString} - RequestId: {RequestId}",
            request.Method,
            request.Path.Value,
            request.QueryString.Value,
            requestId);

        // ヘッダー情報をログ（機密情報を除く）
        var safeHeaders = GetSafeHeaders(request.Headers);
        if (safeHeaders.Count > 0)
        {
            _logger.LogDebug(
                "Request Headers for {RequestId}: {@Headers}",
                requestId,
                safeHeaders);
        }
    }

    private async Task LogRequestCompletedAsync(HttpContext context, string requestId, long durationMs)
    {
        var request = context.Request;
        var response = context.Response;
        
        var logLevel = GetLogLevelForStatusCode(response.StatusCode);
        
        _logger.Log(logLevel,
            "HTTP Request Completed: {Method} {Path} {StatusCode} in {Duration}ms - RequestId: {RequestId}",
            request.Method,
            request.Path.Value,
            response.StatusCode,
            durationMs,
            requestId);

        // エラーレスポンスの場合はセキュリティログも記録
        if (response.StatusCode >= 400)
        {
            await LogErrorResponseAsync(context, requestId, durationMs);
        }
    }

    private async Task LogRequestErrorAsync(HttpContext context, string requestId, Exception exception, long durationMs)
    {
        _logger.LogError(exception,
            "HTTP Request Error: {Method} {Path} failed after {Duration}ms - RequestId: {RequestId}",
            context.Request.Method,
            context.Request.Path.Value,
            durationMs,
            requestId);

        // セキュリティ関連エラーかチェック
        if (IsSecurityReledException(exception))
        {
            await _securityLogService.LogSuspiciousActivityAsync(
                "RequestException",
                $"Exception in request {context.Request.Method} {context.Request.Path}: {exception.GetType().Name}");
        }
    }

    private async Task LogErrorResponseAsync(HttpContext context, string requestId, long durationMs)
    {
        var statusCode = context.Response.StatusCode;
        
        switch (statusCode)
        {
            case 401:
                await _securityLogService.LogUnauthorizedAccessAttemptAsync(
                    context.Request.Path.Value ?? "unknown",
                    context.Request.Method);
                break;
                
            case 403:
                await _securityLogService.LogAccessDeniedAsync(
                    context.User?.Identity?.Name,
                    context.Request.Path.Value ?? "unknown",
                    context.Request.Method,
                    "Forbidden access");
                break;
                
            case 429:
                await _securityLogService.LogRateLimitExceededAsync(
                    context.Request.Path.Value ?? "unknown",
                    1); // 実際のカウントは別途管理
                break;
        }
    }

    private async Task CheckForSuspiciousActivityAsync(HttpContext context)
    {
        var request = context.Request;
        
        // ヘルスチェックや内部エンドポイントはスキップ
        if (IsInternalEndpoint(request.Path.Value))
        {
            return;
        }
        
        // 疑わしいユーザーエージェントチェック
        var userAgent = request.Headers.UserAgent.FirstOrDefault();
        if (IsSuspiciousUserAgent(userAgent))
        {
            await _securityLogService.LogSuspiciousActivityAsync(
                "SuspiciousUserAgent",
                $"Suspicious user agent detected: {userAgent}");
        }
        
        // 疑わしいパスチェック
        if (IsSuspiciousPath(request.Path.Value))
        {
            await _securityLogService.LogSuspiciousActivityAsync(
                "SuspiciousPath",
                $"Suspicious path accessed: {request.Path.Value}");
        }
        
        // SQLインジェクション試行チェック
        if (ContainsSqlInjectionPatterns(request.QueryString.Value))
        {
            await _securityLogService.LogMaliciousRequestAsync(
                "SQLInjectionAttempt",
                request.QueryString.Value ?? "");
        }
    }

    private Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        var sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "X-API-Key", "X-Auth-Token"
        };

        foreach (var header in headers)
        {
            if (!sensitiveHeaders.Contains(header.Key))
            {
                safeHeaders[header.Key] = string.Join(", ", header.Value.ToArray());
            }
            else
            {
                safeHeaders[header.Key] = "***REDACTED***";
            }
        }

        return safeHeaders;
    }

    private LogLevel GetLogLevelForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }

    private bool IsSecurityReledException(Exception exception)
    {
        var securityExceptionTypes = new[]
        {
            "UnauthorizedAccessException",
            "SecurityException",
            "AuthenticationException",
            "AuthorizationException"
        };

        return securityExceptionTypes.Contains(exception.GetType().Name);
    }

    private bool IsInternalEndpoint(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var internalPaths = new[]
        {
            "/health", "/metrics", "/alive", "/_health", 
            "/status", "/ping", "/ready", "/liveness"
        };

        return internalPaths.Any(internalPath =>
            path.Equals(internalPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSuspiciousUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return true; // 空のユーザーエージェントは疑わしい

        var suspiciousPatterns = new[]
        {
            "sqlmap", "nikto", "nmap", "masscan", "zgrab",
            "python-requests", "curl", "wget", "bot", "crawler"
        };

        return suspiciousPatterns.Any(pattern =>
            userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSuspiciousPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var suspiciousPaths = new[]
        {
            "admin", "wp-admin", "wp-login", "phpmyadmin",
            ".env", "config", "backup", "dump", "test",
            "../", "..\\", "%2e%2e", "etc/passwd"
        };

        return suspiciousPaths.Any(suspicious =>
            path.Contains(suspicious, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsSqlInjectionPatterns(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return false;

        var sqlInjectionPatterns = new[]
        {
            "union select", "1=1", "1' or '1'='1", "drop table",
            "insert into", "delete from", "update set", "exec(",
            "xp_", "sp_", "'; --", "/*", "*/"
        };

        var decodedQuery = Uri.UnescapeDataString(queryString);
        
        return sqlInjectionPatterns.Any(pattern =>
            decodedQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 認証ログミドルウェア
/// </summary>
public class AuthenticationLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISecurityLogService _securityLogService;

    public AuthenticationLoggingMiddleware(RequestDelegate next, ISecurityLogService securityLogService)
    {
        _next = next;
        _securityLogService = securityLogService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var wasAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
        var userIdBefore = context.User.Identity?.Name;

        await _next(context);

        var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
        var userIdAfter = context.User.Identity?.Name;

        // 認証状態の変化をログ
        if (!wasAuthenticated && isAuthenticated)
        {
            await _securityLogService.LogLoginSuccessAsync(
                userIdAfter ?? "unknown",
                "JWT"); // 認証方式は設定に応じて変更
        }
        else if (wasAuthenticated && !isAuthenticated)
        {
            await _securityLogService.LogLogoutAsync(
                userIdBefore ?? "unknown");
        }
    }
}

/// <summary>
/// パフォーマンス監視ミドルウェア
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPerformanceLogService _performanceLogService;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        IPerformanceLogService performanceLogService,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _performanceLogService = performanceLogService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // メモリ使用量（開始時）
        var memoryBefore = GC.GetTotalMemory(false);
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // メモリ使用量（終了時）
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryDelta = memoryAfter - memoryBefore;
            
            // 長時間実行される場合のみログ記録
            if (stopwatch.ElapsedMilliseconds > 1000) // 1秒以上
            {
                await _performanceLogService.LogSlowApiRequestAsync(
                    context.Request.Path.Value ?? "unknown",
                    context.Request.Method,
                    stopwatch.ElapsedMilliseconds,
                    $"Memory delta: {memoryDelta} bytes");
            }
            
            // 定期的にシステムメトリクスをログ
            if (ShouldLogSystemMetrics())
            {
                await LogSystemMetricsAsync();
            }
        }
    }

    private bool ShouldLogSystemMetrics()
    {
        // 例：1分に1回程度の頻度でシステムメトリクスをログ
        return DateTime.UtcNow.Second == 0;
    }

    private async Task LogSystemMetricsAsync()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var gcCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            
            await _performanceLogService.LogMemoryUsageAsync(
                totalMemory,
                Environment.WorkingSet,
                gcCollections);

            var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            var cpuUsage = GetCpuUsage(); // 実装が必要
            
            await _performanceLogService.LogCpuUsageAsync(cpuUsage, threadCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log system metrics");
        }
    }

    private double GetCpuUsage()
    {
        // 簡略化された実装。本格的なCPU使用率測定には別途ライブラリが必要
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100;
        }
        catch
        {
            return 0;
        }
    }
}