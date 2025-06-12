using System.Diagnostics;
using TicketManagement.Infrastructure.Logging.Models;

namespace TicketManagement.Infrastructure.Logging.Services;

/// <summary>
/// パフォーマンスログサービスのインターフェース
/// </summary>
public interface IPerformanceLogService
{
    // APIパフォーマンス
    Task LogApiRequestPerformanceAsync(string endpoint, string method, long durationMs, int statusCode, CancellationToken cancellationToken = default);
    Task LogSlowApiRequestAsync(string endpoint, string method, long durationMs, string? details = null, CancellationToken cancellationToken = default);
    
    // データベースパフォーマンス
    Task LogDatabaseQueryPerformanceAsync(string queryType, string tableName, long durationMs, int rowCount = 0, CancellationToken cancellationToken = default);
    Task LogSlowDatabaseQueryAsync(string query, long durationMs, string? executionPlan = null, CancellationToken cancellationToken = default);
    Task LogDatabaseConnectionEventAsync(string eventType, long durationMs, bool success, CancellationToken cancellationToken = default);
    
    // キャッシュパフォーマンス
    Task LogCacheOperationPerformanceAsync(string operation, string key, long durationMs, bool hit, CancellationToken cancellationToken = default);
    Task LogCacheStatisticsAsync(int hitCount, int missCount, double hitRatio, CancellationToken cancellationToken = default);
    
    // SignalRパフォーマンス
    Task LogSignalRConnectionEventAsync(string eventType, string connectionId, long durationMs, CancellationToken cancellationToken = default);
    Task LogSignalRMessagePerformanceAsync(string hubName, string method, int recipientCount, long durationMs, CancellationToken cancellationToken = default);
    
    // システムリソース
    Task LogMemoryUsageAsync(long memoryUsed, long totalMemory, int gcCollections, CancellationToken cancellationToken = default);
    Task LogCpuUsageAsync(double cpuUsage, int threadCount, CancellationToken cancellationToken = default);
    
    // ビジネスプロセスパフォーマンス
    Task LogBusinessProcessPerformanceAsync(string processName, string operation, long durationMs, bool success, Dictionary<string, object>? metrics = null, CancellationToken cancellationToken = default);
    
    // パフォーマンスメトリクス集計
    Task LogPerformanceAggregateAsync(string category, Dictionary<string, object> metrics, CancellationToken cancellationToken = default);
}

/// <summary>
/// パフォーマンスログサービスの実装
/// </summary>
public class PerformanceLogService : IPerformanceLogService
{
    private readonly IStructuredLogger _structuredLogger;
    private readonly ILogEnrichmentService _enrichmentService;

    // パフォーマンス閾値（設定可能にすることを推奨）
    private const long SlowApiThresholdMs = 2000;        // 2秒
    private const long SlowDatabaseThresholdMs = 1000;   // 1秒
    private const long SlowCacheThresholdMs = 100;       // 100ms
    private const long SlowSignalRThresholdMs = 500;     // 500ms

    public PerformanceLogService(IStructuredLogger structuredLogger, ILogEnrichmentService enrichmentService)
    {
        _structuredLogger = structuredLogger;
        _enrichmentService = enrichmentService;
    }

    #region API Performance

    public async Task LogApiRequestPerformanceAsync(string endpoint, string method, long durationMs, int statusCode, CancellationToken cancellationToken = default)
    {
        var isSlowRequest = durationMs > SlowApiThresholdMs;
        
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"{method} {endpoint}",
                DurationMs = durationMs,
                Category = "ApiRequest",
                Metrics = new Dictionary<string, object>
                {
                    ["Endpoint"] = endpoint,
                    ["Method"] = method,
                    ["StatusCode"] = statusCode,
                    ["IsSlowRequest"] = isSlowRequest
                }
            },
            cancellationToken);

        // 遅いリクエストの場合は追加でログ記録
        if (isSlowRequest)
        {
            await LogSlowApiRequestAsync(endpoint, method, durationMs, $"Status: {statusCode}", cancellationToken);
        }
    }

    public async Task LogSlowApiRequestAsync(string endpoint, string method, long durationMs, string? details = null, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"SLOW {method} {endpoint}",
                DurationMs = durationMs,
                Category = "SlowApiRequest",
                Metrics = new Dictionary<string, object>
                {
                    ["Endpoint"] = endpoint,
                    ["Method"] = method,
                    ["Details"] = details ?? "No details provided",
                    ["ThresholdExceededBy"] = durationMs - SlowApiThresholdMs
                }
            },
            cancellationToken);
    }

    #endregion

    #region Database Performance

    public async Task LogDatabaseQueryPerformanceAsync(string queryType, string tableName, long durationMs, int rowCount = 0, CancellationToken cancellationToken = default)
    {
        var isSlowQuery = durationMs > SlowDatabaseThresholdMs;
        
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"DB {queryType}",
                DurationMs = durationMs,
                Category = "DatabaseQuery",
                Metrics = new Dictionary<string, object>
                {
                    ["QueryType"] = queryType,
                    ["TableName"] = tableName,
                    ["RowCount"] = rowCount,
                    ["IsSlowQuery"] = isSlowQuery,
                    ["RowsPerMs"] = durationMs > 0 ? (double)rowCount / durationMs : 0
                }
            },
            cancellationToken);
    }

    public async Task LogSlowDatabaseQueryAsync(string query, long durationMs, string? executionPlan = null, CancellationToken cancellationToken = default)
    {
        // クエリをサニタイズ（機密情報の除去）
        var sanitizedQuery = SanitizeDatabaseQuery(query);
        
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = "SLOW DB Query",
                DurationMs = durationMs,
                Category = "SlowDatabaseQuery",
                Metrics = new Dictionary<string, object>
                {
                    ["SanitizedQuery"] = sanitizedQuery,
                    ["ExecutionPlan"] = executionPlan ?? "Not available",
                    ["ThresholdExceededBy"] = durationMs - SlowDatabaseThresholdMs,
                    ["QueryLength"] = query.Length
                }
            },
            cancellationToken);
    }

    public async Task LogDatabaseConnectionEventAsync(string eventType, long durationMs, bool success, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"DB Connection {eventType}",
                DurationMs = durationMs,
                Category = "DatabaseConnection",
                Metrics = new Dictionary<string, object>
                {
                    ["EventType"] = eventType,
                    ["Success"] = success,
                    ["ConnectionTime"] = DateTime.UtcNow
                }
            },
            cancellationToken);
    }

    #endregion

    #region Cache Performance

    public async Task LogCacheOperationPerformanceAsync(string operation, string key, long durationMs, bool hit, CancellationToken cancellationToken = default)
    {
        var isSlowOperation = durationMs > SlowCacheThresholdMs;
        
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"Cache {operation}",
                DurationMs = durationMs,
                Category = "CacheOperation",
                Metrics = new Dictionary<string, object>
                {
                    ["Operation"] = operation,
                    ["Key"] = MaskCacheKey(key),
                    ["Hit"] = hit,
                    ["IsSlowOperation"] = isSlowOperation
                }
            },
            cancellationToken);
    }

    public async Task LogCacheStatisticsAsync(int hitCount, int missCount, double hitRatio, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = "Cache Statistics",
                DurationMs = 0, // 統計情報なので実行時間は0
                Category = "CacheStatistics",
                Metrics = new Dictionary<string, object>
                {
                    ["HitCount"] = hitCount,
                    ["MissCount"] = missCount,
                    ["TotalRequests"] = hitCount + missCount,
                    ["HitRatio"] = hitRatio,
                    ["StatisticsTime"] = DateTime.UtcNow
                }
            },
            cancellationToken);
    }

    #endregion

    #region SignalR Performance

    public async Task LogSignalRConnectionEventAsync(string eventType, string connectionId, long durationMs, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"SignalR {eventType}",
                DurationMs = durationMs,
                Category = "SignalRConnection",
                Metrics = new Dictionary<string, object>
                {
                    ["EventType"] = eventType,
                    ["ConnectionId"] = MaskConnectionId(connectionId),
                    ["IsSlowOperation"] = durationMs > SlowSignalRThresholdMs
                }
            },
            cancellationToken);
    }

    public async Task LogSignalRMessagePerformanceAsync(string hubName, string method, int recipientCount, long durationMs, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"SignalR {hubName}.{method}",
                DurationMs = durationMs,
                Category = "SignalRMessage",
                Metrics = new Dictionary<string, object>
                {
                    ["HubName"] = hubName,
                    ["Method"] = method,
                    ["RecipientCount"] = recipientCount,
                    ["MessageThroughput"] = durationMs > 0 ? (double)recipientCount / durationMs * 1000 : 0, // recipients per second
                    ["IsSlowOperation"] = durationMs > SlowSignalRThresholdMs
                }
            },
            cancellationToken);
    }

    #endregion

    #region System Resources

    public async Task LogMemoryUsageAsync(long memoryUsed, long totalMemory, int gcCollections, CancellationToken cancellationToken = default)
    {
        var memoryUsagePercentage = totalMemory > 0 ? (double)memoryUsed / totalMemory * 100 : 0;
        
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = "Memory Usage",
                DurationMs = 0, // システムメトリクスなので実行時間は0
                Category = "SystemMemory",
                Metrics = new Dictionary<string, object>
                {
                    ["MemoryUsedMB"] = memoryUsed / (1024 * 1024),
                    ["TotalMemoryMB"] = totalMemory / (1024 * 1024),
                    ["MemoryUsagePercentage"] = Math.Round(memoryUsagePercentage, 2),
                    ["GCCollections"] = gcCollections,
                    ["MeasurementTime"] = DateTime.UtcNow
                }
            },
            cancellationToken);
    }

    public async Task LogCpuUsageAsync(double cpuUsage, int threadCount, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = "CPU Usage",
                DurationMs = 0, // システムメトリクスなので実行時間は0
                Category = "SystemCPU",
                Metrics = new Dictionary<string, object>
                {
                    ["CpuUsagePercentage"] = Math.Round(cpuUsage, 2),
                    ["ThreadCount"] = threadCount,
                    ["ProcessorCount"] = Environment.ProcessorCount,
                    ["MeasurementTime"] = DateTime.UtcNow
                }
            },
            cancellationToken);
    }

    #endregion

    #region Business Process Performance

    public async Task LogBusinessProcessPerformanceAsync(string processName, string operation, long durationMs, bool success, Dictionary<string, object>? metrics = null, CancellationToken cancellationToken = default)
    {
        var performanceMetrics = new Dictionary<string, object>
        {
            ["ProcessName"] = processName,
            ["Operation"] = operation,
            ["Success"] = success
        };

        // 追加メトリクスをマージ
        if (metrics != null)
        {
            foreach (var metric in metrics)
            {
                performanceMetrics[metric.Key] = metric.Value;
            }
        }

        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"{processName} - {operation}",
                DurationMs = durationMs,
                Category = "BusinessProcess",
                Metrics = performanceMetrics
            },
            cancellationToken);
    }

    #endregion

    #region Performance Aggregates

    public async Task LogPerformanceAggregateAsync(string category, Dictionary<string, object> metrics, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogPerformanceEventAsync(
            new PerformanceLogEvent
            {
                Operation = $"Performance Aggregate - {category}",
                DurationMs = 0, // 集計データなので実行時間は0
                Category = "PerformanceAggregate",
                Metrics = new Dictionary<string, object>(metrics)
                {
                    ["AggregateCategory"] = category,
                    ["AggregateTime"] = DateTime.UtcNow
                }
            },
            cancellationToken);
    }

    #endregion

    #region Helper Methods

    private string SanitizeDatabaseQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
            return "empty";

        // クエリから機密情報を除去
        var sensitivePatterns = new[]
        {
            @"password\s*=\s*'[^']*'",
            @"password\s*=\s*""[^""]*""",
            @"token\s*=\s*'[^']*'",
            @"secret\s*=\s*'[^']*'"
        };

        var sanitized = query;
        foreach (var pattern in sensitivePatterns)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized, pattern, "***REDACTED***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // 長いクエリを切り詰め
        const int maxLength = 1000;
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength] + "... (truncated)";
        }

        return sanitized;
    }

    private string MaskCacheKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "empty";

        // キャッシュキーから機密情報をマスク
        if (key.Contains("user:", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("session:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = key.Split(':');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}:***";
            }
        }

        return key;
    }

    private string MaskConnectionId(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
            return "empty";

        // 接続IDの一部をマスク
        return connectionId.Length > 8 
            ? connectionId[..4] + "***" + connectionId[^4..] 
            : "***";
    }

    #endregion
}

/// <summary>
/// パフォーマンス測定用のヘルパークラス
/// </summary>
public class PerformanceTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly Func<long, Task> _logAction;

    public PerformanceTimer(Func<long, Task> logAction)
    {
        _logAction = logAction;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _ = Task.Run(() => _logAction(_stopwatch.ElapsedMilliseconds));
    }
}

/// <summary>
/// パフォーマンスタイマー拡張メソッド
/// </summary>
public static class PerformanceTimerExtensions
{
    /// <summary>
    /// API操作のパフォーマンスを測定
    /// </summary>
    public static PerformanceTimer MeasureApiPerformance(
        this IPerformanceLogService performanceLogService,
        string endpoint,
        string method,
        int statusCode)
    {
        return new PerformanceTimer(duration =>
            performanceLogService.LogApiRequestPerformanceAsync(endpoint, method, duration, statusCode));
    }

    /// <summary>
    /// データベース操作のパフォーマンスを測定
    /// </summary>
    public static PerformanceTimer MeasureDatabasePerformance(
        this IPerformanceLogService performanceLogService,
        string queryType,
        string tableName,
        int rowCount = 0)
    {
        return new PerformanceTimer(duration =>
            performanceLogService.LogDatabaseQueryPerformanceAsync(queryType, tableName, duration, rowCount));
    }

    /// <summary>
    /// ビジネスプロセスのパフォーマンスを測定
    /// </summary>
    public static PerformanceTimer MeasureBusinessProcessPerformance(
        this IPerformanceLogService performanceLogService,
        string processName,
        string operation,
        bool success = true,
        Dictionary<string, object>? metrics = null)
    {
        return new PerformanceTimer(duration =>
            performanceLogService.LogBusinessProcessPerformanceAsync(processName, operation, duration, success, metrics));
    }
}