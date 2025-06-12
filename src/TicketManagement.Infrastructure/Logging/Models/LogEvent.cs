namespace TicketManagement.Infrastructure.Logging.Models;

/// <summary>
/// 構造化ログイベントの基底クラス
/// </summary>
public abstract class LogEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
}

/// <summary>
/// セキュリティ関連ログイベント
/// </summary>
public class SecurityLogEvent : LogEvent
{
    public required string EventType { get; set; }
    public string? Resource { get; set; }
    public string? Action { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 監査ログイベント
/// </summary>
public class AuditLogEvent : LogEvent
{
    public required string Operation { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new();
}

/// <summary>
/// ユーザーアクティビティログイベント
/// </summary>
public class UserActivityLogEvent : LogEvent
{
    public required string ActivityType { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// パフォーマンスログイベント
/// </summary>
public class PerformanceLogEvent : LogEvent
{
    public required string Operation { get; set; }
    public long DurationMs { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public bool IsSlowOperation => DurationMs > 1000; // 1秒以上は遅いとみなす
}

/// <summary>
/// ビジネスロジックログイベント
/// </summary>
public class BusinessLogEvent : LogEvent
{
    public required string BusinessProcess { get; set; }
    public required string Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// ログイベントタイプ定数
/// </summary>
public static class LogEventTypes
{
    // セキュリティイベント
    public const string LOGIN_SUCCESS = "LoginSuccess";
    public const string LOGIN_FAILURE = "LoginFailure";
    public const string LOGOUT = "Logout";
    public const string ACCESS_DENIED = "AccessDenied";
    public const string UNAUTHORIZED_ACCESS = "UnauthorizedAccess";
    public const string RATE_LIMIT_EXCEEDED = "RateLimitExceeded";
    public const string SUSPICIOUS_ACTIVITY = "SuspiciousActivity";
    
    // 監査イベント
    public const string DATA_CREATE = "DataCreate";
    public const string DATA_UPDATE = "DataUpdate";
    public const string DATA_DELETE = "DataDelete";
    public const string DATA_EXPORT = "DataExport";
    public const string PERMISSION_CHANGE = "PermissionChange";
    
    // ユーザーアクティビティ
    public const string PAGE_VIEW = "PageView";
    public const string FILE_DOWNLOAD = "FileDownload";
    public const string SEARCH_PERFORMED = "SearchPerformed";
    public const string REPORT_GENERATED = "ReportGenerated";
    
    // ビジネスプロセス
    public const string TICKET_CREATED = "TicketCreated";
    public const string TICKET_UPDATED = "TicketUpdated";
    public const string TICKET_STATUS_CHANGED = "TicketStatusChanged";
    public const string PROJECT_MEMBER_ADDED = "ProjectMemberAdded";
    public const string NOTIFICATION_SENT = "NotificationSent";
}