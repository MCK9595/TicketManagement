using Microsoft.Extensions.Logging;
using System.Text.Json;
using TicketManagement.Infrastructure.Logging.Models;

namespace TicketManagement.Infrastructure.Logging.Services;

/// <summary>
/// 構造化ログサービスのインターフェース
/// </summary>
public interface IStructuredLogger
{
    // セキュリティログ
    Task LogSecurityEventAsync(SecurityLogEvent logEvent, CancellationToken cancellationToken = default);
    
    // 監査ログ
    Task LogAuditEventAsync(AuditLogEvent logEvent, CancellationToken cancellationToken = default);
    
    // ユーザーアクティビティログ
    Task LogUserActivityAsync(UserActivityLogEvent logEvent, CancellationToken cancellationToken = default);
    
    // パフォーマンスログ
    Task LogPerformanceEventAsync(PerformanceLogEvent logEvent, CancellationToken cancellationToken = default);
    
    // ビジネスログ
    Task LogBusinessEventAsync(BusinessLogEvent logEvent, CancellationToken cancellationToken = default);
    
    // 汎用構造化ログ
    Task LogStructuredAsync<T>(LogLevel logLevel, string message, T data, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// 構造化ログサービスの実装
/// </summary>
public class StructuredLogger : IStructuredLogger
{
    private readonly ILogger<StructuredLogger> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public StructuredLogger(ILogger<StructuredLogger> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task LogSecurityEventAsync(SecurityLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var logLevel = logEvent.Success ? LogLevel.Information : LogLevel.Warning;
        var message = $"Security Event: {logEvent.EventType}";
        
        // 機密情報をフィルタリング
        var sanitizedEvent = SanitizeSecurityEvent(logEvent);
        
        _logger.Log(logLevel, message + " {@SecurityEvent}", sanitizedEvent);
        
        // 重要なセキュリティイベントは別途記録
        if (IsHighRiskSecurityEvent(logEvent.EventType))
        {
            await LogHighRiskSecurityEventAsync(sanitizedEvent, cancellationToken);
        }
    }

    public async Task LogAuditEventAsync(AuditLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var message = $"Audit: {logEvent.Operation} on {logEvent.EntityType}";
        
        // PIIを除外してログ記録
        var sanitizedEvent = SanitizeAuditEvent(logEvent);
        
        _logger.LogInformation(message + " {@AuditEvent}", sanitizedEvent);
        
        await Task.CompletedTask;
    }

    public async Task LogUserActivityAsync(UserActivityLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var message = $"User Activity: {logEvent.ActivityType}";
        
        var sanitizedEvent = SanitizeUserActivityEvent(logEvent);
        
        _logger.LogInformation(message + " {@UserActivity}", sanitizedEvent);
        
        await Task.CompletedTask;
    }

    public async Task LogPerformanceEventAsync(PerformanceLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var logLevel = logEvent.IsSlowOperation ? LogLevel.Warning : LogLevel.Information;
        var message = $"Performance: {logEvent.Operation} took {logEvent.DurationMs}ms";
        
        _logger.Log(logLevel, message + " {@PerformanceEvent}", logEvent);
        
        await Task.CompletedTask;
    }

    public async Task LogBusinessEventAsync(BusinessLogEvent logEvent, CancellationToken cancellationToken = default)
    {
        var logLevel = logEvent.Success ? LogLevel.Information : LogLevel.Error;
        var message = $"Business: {logEvent.BusinessProcess} - {logEvent.Action}";
        
        var sanitizedEvent = SanitizeBusinessEvent(logEvent);
        
        _logger.Log(logLevel, message + " {@BusinessEvent}", sanitizedEvent);
        
        await Task.CompletedTask;
    }

    public async Task LogStructuredAsync<T>(LogLevel logLevel, string message, T data, CancellationToken cancellationToken = default) where T : class
    {
        _logger.Log(logLevel, message + " {@Data}", data);
        await Task.CompletedTask;
    }

    #region Private Methods

    private SecurityLogEvent SanitizeSecurityEvent(SecurityLogEvent logEvent)
    {
        // セキュリティログから機密情報を除外
        var sanitized = new SecurityLogEvent
        {
            EventType = logEvent.EventType,
            Resource = logEvent.Resource,
            Action = logEvent.Action,
            Success = logEvent.Success,
            FailureReason = logEvent.FailureReason,
            UserId = logEvent.UserId,
            UserName = MaskPersonalData(logEvent.UserName),
            CorrelationId = logEvent.CorrelationId,
            IpAddress = MaskIpAddress(logEvent.IpAddress),
            UserAgent = TruncateUserAgent(logEvent.UserAgent),
            SessionId = logEvent.SessionId,
            Timestamp = logEvent.Timestamp,
            Metadata = FilterSensitiveMetadata(logEvent.Metadata)
        };

        return sanitized;
    }

    private AuditLogEvent SanitizeAuditEvent(AuditLogEvent logEvent)
    {
        return new AuditLogEvent
        {
            Operation = logEvent.Operation,
            EntityType = logEvent.EntityType,
            EntityId = logEvent.EntityId,
            OldValue = SanitizeObjectData(logEvent.OldValue),
            NewValue = SanitizeObjectData(logEvent.NewValue),
            Changes = FilterSensitiveMetadata(logEvent.Changes),
            UserId = logEvent.UserId,
            UserName = MaskPersonalData(logEvent.UserName),
            CorrelationId = logEvent.CorrelationId,
            IpAddress = MaskIpAddress(logEvent.IpAddress),
            Timestamp = logEvent.Timestamp
        };
    }

    private UserActivityLogEvent SanitizeUserActivityEvent(UserActivityLogEvent logEvent)
    {
        return new UserActivityLogEvent
        {
            ActivityType = logEvent.ActivityType,
            ResourceType = logEvent.ResourceType,
            ResourceId = logEvent.ResourceId,
            Description = logEvent.Description,
            Parameters = FilterSensitiveMetadata(logEvent.Parameters),
            UserId = logEvent.UserId,
            UserName = MaskPersonalData(logEvent.UserName),
            CorrelationId = logEvent.CorrelationId,
            IpAddress = MaskIpAddress(logEvent.IpAddress),
            Timestamp = logEvent.Timestamp
        };
    }

    private BusinessLogEvent SanitizeBusinessEvent(BusinessLogEvent logEvent)
    {
        return new BusinessLogEvent
        {
            BusinessProcess = logEvent.BusinessProcess,
            Action = logEvent.Action,
            EntityType = logEvent.EntityType,
            EntityId = logEvent.EntityId,
            Success = logEvent.Success,
            ErrorMessage = logEvent.ErrorMessage,
            Context = FilterSensitiveMetadata(logEvent.Context),
            UserId = logEvent.UserId,
            UserName = MaskPersonalData(logEvent.UserName),
            CorrelationId = logEvent.CorrelationId,
            Timestamp = logEvent.Timestamp
        };
    }

    private string? MaskPersonalData(string? data)
    {
        if (string.IsNullOrEmpty(data)) return data;
        
        // 個人情報をマスク（例：メールアドレス、名前の一部）
        if (data.Contains('@'))
        {
            var parts = data.Split('@');
            if (parts.Length == 2)
            {
                var name = parts[0];
                var domain = parts[1];
                var maskedName = name.Length > 2 ? name[..2] + "***" : "***";
                return $"{maskedName}@{domain}";
            }
        }
        
        return data.Length > 3 ? data[..3] + "***" : "***";
    }

    private string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress)) return ipAddress;
        
        // IPv4のマスク（最後のオクテットを隠す）
        var parts = ipAddress.Split('.');
        if (parts.Length == 4)
        {
            return $"{parts[0]}.{parts[1]}.{parts[2]}.***";
        }
        
        // IPv6のマスク（後半を隠す）
        if (ipAddress.Contains(':'))
        {
            var segments = ipAddress.Split(':');
            if (segments.Length > 4)
            {
                return string.Join(":", segments[..4]) + ":***";
            }
        }
        
        return "***";
    }

    private string? TruncateUserAgent(string? userAgent)
    {
        return userAgent?.Length > 200 ? userAgent[..200] + "..." : userAgent;
    }

    private Dictionary<string, object> FilterSensitiveMetadata(Dictionary<string, object> metadata)
    {
        var filtered = new Dictionary<string, object>();
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "token", "secret", "key", "authorization", "cookie",
            "email", "phone", "ssn", "creditcard", "bankaccount"
        };

        foreach (var kvp in metadata)
        {
            if (sensitiveKeys.Any(key => kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
            {
                filtered[kvp.Key] = "***REDACTED***";
            }
            else
            {
                filtered[kvp.Key] = kvp.Value;
            }
        }

        return filtered;
    }

    private object? SanitizeObjectData(object? data)
    {
        if (data == null) return null;

        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var document = JsonDocument.Parse(json);
            
            // オブジェクトから機密データを除外
            return FilterSensitiveJsonData(document.RootElement);
        }
        catch
        {
            return "***SANITIZATION_ERROR***";
        }
    }

    private object FilterSensitiveJsonData(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var filtered = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    var key = property.Name;
                    if (IsSensitiveProperty(key))
                    {
                        filtered[key] = "***REDACTED***";
                    }
                    else
                    {
                        filtered[key] = FilterSensitiveJsonData(property.Value);
                    }
                }
                return filtered;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(FilterSensitiveJsonData)
                    .ToArray();

            default:
                return element.ToString();
        }
    }

    private bool IsSensitiveProperty(string propertyName)
    {
        var sensitivePatterns = new[]
        {
            "password", "token", "secret", "key", "authorization",
            "email", "phone", "ssn", "creditcard", "bankaccount"
        };

        return sensitivePatterns.Any(pattern => 
            propertyName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsHighRiskSecurityEvent(string eventType)
    {
        var highRiskEvents = new[]
        {
            LogEventTypes.LOGIN_FAILURE,
            LogEventTypes.ACCESS_DENIED,
            LogEventTypes.UNAUTHORIZED_ACCESS,
            LogEventTypes.RATE_LIMIT_EXCEEDED,
            LogEventTypes.SUSPICIOUS_ACTIVITY
        };

        return highRiskEvents.Contains(eventType);
    }

    private async Task LogHighRiskSecurityEventAsync(SecurityLogEvent logEvent, CancellationToken cancellationToken)
    {
        // 高リスクセキュリティイベントの特別処理
        // 例：別のログストリーム、アラート送信、管理者通知など
        _logger.LogCritical("HIGH RISK SECURITY EVENT: {EventType} for user {UserId} from {IpAddress}",
            logEvent.EventType, logEvent.UserId, logEvent.IpAddress);
        
        await Task.CompletedTask;
    }

    #endregion
}