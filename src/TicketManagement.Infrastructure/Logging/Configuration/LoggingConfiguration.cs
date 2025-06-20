using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketManagement.Infrastructure.Logging.Services;
using TicketManagement.Infrastructure.Logging.Middleware;

namespace TicketManagement.Infrastructure.Logging.Configuration;

/// <summary>
/// ログ設定オプション
/// </summary>
public class LoggingOptions
{
    public const string SectionName = "Logging";
    
    public bool EnableStructuredLogging { get; set; } = true;
    public bool EnableSecurityLogging { get; set; } = true;
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableRequestResponseLogging { get; set; } = true;
    
    public SecurityLoggingOptions Security { get; set; } = new();
    public AuditLoggingOptions Audit { get; set; } = new();
    public PerformanceLoggingOptions Performance { get; set; } = new();
    public DataProtectionOptions DataProtection { get; set; } = new();
}

/// <summary>
/// セキュリティログ設定
/// </summary>
public class SecurityLoggingOptions
{
    public bool LogFailedAuthentications { get; set; } = true;
    public bool LogAccessDenials { get; set; } = true;
    public bool LogSuspiciousActivity { get; set; } = true;
    public bool LogRateLimitViolations { get; set; } = true;
    public bool LogInputValidationFailures { get; set; } = true;
    public string[] SuspiciousUserAgentPatterns { get; set; } = Array.Empty<string>();
    public string[] SuspiciousPathPatterns { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 監査ログ設定
/// </summary>
public class AuditLoggingOptions
{
    public bool LogDataCreation { get; set; } = true;
    public bool LogDataModification { get; set; } = true;
    public bool LogDataDeletion { get; set; } = true;
    public bool LogDataAccess { get; set; } = true;
    public bool LogPermissionChanges { get; set; } = true;
    public bool LogConfigurationChanges { get; set; } = true;
    public int RetentionDays { get; set; } = 365;
    public string[] ExcludedEntities { get; set; } = Array.Empty<string>();
}

/// <summary>
/// パフォーマンスログ設定
/// </summary>
public class PerformanceLoggingOptions
{
    public bool LogSlowRequests { get; set; } = true;
    public bool LogSlowQueries { get; set; } = true;
    public bool LogSystemMetrics { get; set; } = true;
    public long SlowRequestThresholdMs { get; set; } = 2000;
    public long SlowQueryThresholdMs { get; set; } = 1000;
    public int SystemMetricsIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// データ保護設定
/// </summary>
public class DataProtectionOptions
{
    public bool MaskPersonalData { get; set; } = true;
    public bool MaskSensitiveData { get; set; } = true;
    public bool LogSensitiveDataAccess { get; set; } = true;
    public string[] SensitiveFieldPatterns { get; set; } = 
    {
        "password", "token", "secret", "key", "email", "phone", "ssn"
    };
}

/// <summary>
/// ログ設定拡張メソッド
/// </summary>
public static class LoggingConfigurationExtensions
{
    /// <summary>
    /// 構造化ログシステムを設定
    /// </summary>
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 設定オプションを登録
        services.Configure<LoggingOptions>(configuration.GetSection(LoggingOptions.SectionName));
        
        // ログサービスを登録
        services.AddTransient<IStructuredLogger, StructuredLogger>();
        services.AddTransient<ILogEnrichmentService, LogEnrichmentService>();
        services.AddTransient<IAuditLogService, AuditLogService>();
        services.AddTransient<ISecurityLogService, SecurityLogService>();
        services.AddTransient<IPerformanceLogService, PerformanceLogService>();
        
        // HTTPコンテキストアクセッサーを登録（ログエンリッチメントに必要）
        services.AddHttpContextAccessor();
        
        return services;
    }

    /// <summary>
    /// ログコレクターを設定（将来の拡張用）
    /// </summary>
    public static IServiceCollection AddLogCollectors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 将来的にElasticsearch、Splunk、Azure Log Analyticsなどへの出力を追加
        return services;
    }

    /// <summary>
    /// ログアラートを設定（将来の拡張用）
    /// </summary>
    public static IServiceCollection AddLogAlerting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 将来的にアラート機能を追加
        return services;
    }
}

/// <summary>
/// ログミドルウェア設定拡張メソッド
/// </summary>
public static class LoggingMiddlewareExtensions
{
    /// <summary>
    /// 強化されたログミドルウェアを追加
    /// </summary>
    public static IApplicationBuilder UseEnhancedLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<EnhancedLoggingMiddleware>();
        return app;
    }

    /// <summary>
    /// リクエストログミドルウェアを追加
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();
        return app;
    }

    /// <summary>
    /// 認証ログミドルウェアを追加
    /// </summary>
    public static IApplicationBuilder UseAuthenticationLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<AuthenticationLoggingMiddleware>();
        return app;
    }

    /// <summary>
    /// パフォーマンス監視ミドルウェアを追加
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder app)
    {
        app.UseMiddleware<PerformanceMonitoringMiddleware>();
        return app;
    }

    /// <summary>
    /// レート制限ミドルウェアを追加
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        app.UseMiddleware<RateLimitingMiddleware>();
        return app;
    }
}

/// <summary>
/// ログレベル管理
/// </summary>
public static class LogLevels
{
    public static class Security
    {
        public const LogLevel LoginSuccess = LogLevel.Information;
        public const LogLevel LoginFailure = LogLevel.Warning;
        public const LogLevel AccessDenied = LogLevel.Warning;
        public const LogLevel SuspiciousActivity = LogLevel.Error;
        public const LogLevel DataBreach = LogLevel.Critical;
    }

    public static class Audit
    {
        public const LogLevel DataCreated = LogLevel.Information;
        public const LogLevel DataModified = LogLevel.Information;
        public const LogLevel DataDeleted = LogLevel.Warning;
        public const LogLevel PermissionChanged = LogLevel.Warning;
        public const LogLevel ConfigurationChanged = LogLevel.Warning;
    }

    public static class Performance
    {
        public const LogLevel NormalOperation = LogLevel.Debug;
        public const LogLevel SlowOperation = LogLevel.Warning;
        public const LogLevel FailedOperation = LogLevel.Error;
        public const LogLevel CriticalPerformance = LogLevel.Critical;
    }
}

/// <summary>
/// ログカテゴリ定数
/// </summary>
public static class LogCategories
{
    public const string Security = "Security";
    public const string Audit = "Audit";
    public const string Performance = "Performance";
    public const string Business = "Business";
    public const string System = "System";
    public const string UserActivity = "UserActivity";
    public const string DataAccess = "DataAccess";
    public const string Integration = "Integration";
}

/// <summary>
/// ログフィルタ設定
/// </summary>
public class LogFilterConfiguration
{
    public static void ConfigureFilters(ILoggingBuilder builder)
    {
        // Microsoft関連のログレベルを調整
        builder.AddFilter("Microsoft", LogLevel.Warning);
        builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        
        // アプリケーション固有のログレベル
        builder.AddFilter("TicketManagement", LogLevel.Debug);
        builder.AddFilter("TicketManagement.Infrastructure.Logging", LogLevel.Information);
        
        // セキュリティログは常に記録
        builder.AddFilter("TicketManagement.Infrastructure.Logging.Services.SecurityLogService", LogLevel.Trace);
        
        // パフォーマンスログは本番環境では情報レベル以上
        builder.AddFilter("TicketManagement.Infrastructure.Logging.Services.PerformanceLogService", LogLevel.Information);
    }
}

/// <summary>
/// ログ出力先設定
/// </summary>
public static class LogOutputConfiguration
{
    public static void ConfigureConsoleOutput(ILoggingBuilder builder)
    {
        builder.AddConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        });
    }

    public static void ConfigureDebugOutput(ILoggingBuilder builder)
    {
        builder.AddDebug();
    }

    public static void ConfigureEventSourceOutput(ILoggingBuilder builder)
    {
        builder.AddEventSourceLogger();
    }
}

/// <summary>
/// ログコンテキスト設定
/// </summary>
public static class LogScopeConfiguration
{
    public static IDisposable CreateRequestScope(ILogger logger, string requestId, string? userId = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Timestamp"] = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(userId))
        {
            scope["UserId"] = userId;
        }

        return logger.BeginScope(scope);
    }

    public static IDisposable CreateOperationScope(ILogger logger, string operationName, string? entityId = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["Operation"] = operationName,
            ["Timestamp"] = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(entityId))
        {
            scope["EntityId"] = entityId;
        }

        return logger.BeginScope(scope);
    }
}