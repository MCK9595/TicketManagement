using Microsoft.AspNetCore.Http;
using TicketManagement.Infrastructure.Logging.Models;

namespace TicketManagement.Infrastructure.Logging.Services;

/// <summary>
/// セキュリティログサービスのインターフェース
/// </summary>
public interface ISecurityLogService
{
    // 認証関連ログ
    Task LogLoginSuccessAsync(string userId, string authMethod, CancellationToken cancellationToken = default);
    Task LogLoginFailureAsync(string? userId, string reason, string authMethod, CancellationToken cancellationToken = default);
    Task LogLogoutAsync(string userId, CancellationToken cancellationToken = default);
    Task LogTokenRefreshAsync(string userId, CancellationToken cancellationToken = default);
    
    // 認可関連ログ
    Task LogAccessDeniedAsync(string? userId, string resource, string action, string reason, CancellationToken cancellationToken = default);
    Task LogUnauthorizedAccessAttemptAsync(string resource, string action, CancellationToken cancellationToken = default);
    Task LogPrivilegeEscalationAttemptAsync(string userId, string attemptedRole, string currentRole, CancellationToken cancellationToken = default);
    
    // セキュリティ違反ログ
    Task LogRateLimitExceededAsync(string endpoint, int requestCount, CancellationToken cancellationToken = default);
    Task LogSuspiciousActivityAsync(string activityType, string details, CancellationToken cancellationToken = default);
    Task LogSecurityHeaderViolationAsync(string headerType, string violationDetails, CancellationToken cancellationToken = default);
    Task LogInputValidationFailureAsync(string inputField, string violationType, string inputValue, CancellationToken cancellationToken = default);
    
    // データセキュリティログ
    Task LogSensitiveDataAccessAsync(string dataType, string operation, string? entityId = null, CancellationToken cancellationToken = default);
    Task LogDataLeakageAttemptAsync(string dataType, string leakageMethod, CancellationToken cancellationToken = default);
    Task LogEncryptionEventAsync(string operation, string dataType, bool success, CancellationToken cancellationToken = default);
    
    // システムセキュリティログ
    Task LogConfigurationChangeAsync(string configType, string oldValue, string newValue, CancellationToken cancellationToken = default);
    Task LogSecurityScanDetectedAsync(string scanType, string sourceIp, CancellationToken cancellationToken = default);
    Task LogMaliciousRequestAsync(string requestType, string payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// セキュリティログサービスの実装
/// </summary>
public class SecurityLogService : ISecurityLogService
{
    private readonly IStructuredLogger _structuredLogger;
    private readonly ILogEnrichmentService _enrichmentService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SecurityLogService(
        IStructuredLogger structuredLogger, 
        ILogEnrichmentService enrichmentService,
        IHttpContextAccessor httpContextAccessor)
    {
        _structuredLogger = structuredLogger;
        _enrichmentService = enrichmentService;
        _httpContextAccessor = httpContextAccessor;
    }

    #region Authentication Logs

    public async Task LogLoginSuccessAsync(string userId, string authMethod, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.LOGIN_SUCCESS,
            "Authentication",
            "Login",
            true,
            metadata: new Dictionary<string, object>
            {
                ["AuthMethod"] = authMethod,
                ["TargetUserId"] = userId,
                ["LoginTime"] = DateTime.UtcNow
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogLoginFailureAsync(string? userId, string reason, string authMethod, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.LOGIN_FAILURE,
            "Authentication",
            "Login",
            false,
            reason,
            new Dictionary<string, object>
            {
                ["AuthMethod"] = authMethod,
                ["TargetUserId"] = userId ?? "unknown",
                ["FailureReason"] = reason,
                ["AttemptTime"] = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task LogLogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.LOGOUT,
            "Authentication",
            "Logout",
            true,
            metadata: new Dictionary<string, object>
            {
                ["TargetUserId"] = userId,
                ["LogoutTime"] = DateTime.UtcNow
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogTokenRefreshAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "TokenRefresh",
            "Authentication",
            "TokenRefresh",
            true,
            metadata: new Dictionary<string, object>
            {
                ["TargetUserId"] = userId,
                ["RefreshTime"] = DateTime.UtcNow
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Authorization Logs

    public async Task LogAccessDeniedAsync(string? userId, string resource, string action, string reason, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.ACCESS_DENIED,
            resource,
            action,
            false,
            reason,
            new Dictionary<string, object>
            {
                ["TargetUserId"] = userId ?? "anonymous",
                ["DenialReason"] = reason,
                ["RequestedResource"] = resource,
                ["RequestedAction"] = action
            },
            cancellationToken);
    }

    public async Task LogUnauthorizedAccessAttemptAsync(string resource, string action, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.UNAUTHORIZED_ACCESS,
            resource,
            action,
            false,
            "Unauthorized access attempt",
            new Dictionary<string, object>
            {
                ["RequestedResource"] = resource,
                ["RequestedAction"] = action,
                ["Referer"] = GetRefererHeader(),
                ["RequestPath"] = GetRequestPath()
            },
            cancellationToken);
    }

    public async Task LogPrivilegeEscalationAttemptAsync(string userId, string attemptedRole, string currentRole, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "PrivilegeEscalationAttempt",
            "Authorization",
            "RoleEscalation",
            false,
            "User attempted to escalate privileges",
            new Dictionary<string, object>
            {
                ["TargetUserId"] = userId,
                ["AttemptedRole"] = attemptedRole,
                ["CurrentRole"] = currentRole
            },
            cancellationToken);
    }

    #endregion

    #region Security Violation Logs

    public async Task LogRateLimitExceededAsync(string endpoint, int requestCount, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.RATE_LIMIT_EXCEEDED,
            endpoint,
            "Request",
            false,
            "Rate limit exceeded",
            new Dictionary<string, object>
            {
                ["Endpoint"] = endpoint,
                ["RequestCount"] = requestCount,
                ["TimeWindow"] = "5 minutes",
                ["UserAgent"] = GetUserAgent()
            },
            cancellationToken);
    }

    public async Task LogSuspiciousActivityAsync(string activityType, string details, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.SUSPICIOUS_ACTIVITY,
            activityType,
            "Monitor",
            false,
            details,
            new Dictionary<string, object>
            {
                ["ActivityType"] = activityType,
                ["Details"] = details,
                ["DetectionTime"] = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task LogSecurityHeaderViolationAsync(string headerType, string violationDetails, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "SecurityHeaderViolation",
            "SecurityHeaders",
            "Validation",
            false,
            violationDetails,
            new Dictionary<string, object>
            {
                ["HeaderType"] = headerType,
                ["ViolationDetails"] = violationDetails,
                ["RequestPath"] = GetRequestPath()
            },
            cancellationToken);
    }

    public async Task LogInputValidationFailureAsync(string inputField, string violationType, string inputValue, CancellationToken cancellationToken = default)
    {
        // 入力値をサニタイズして記録
        var sanitizedInput = SanitizeInputForLogging(inputValue);
        
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "InputValidationFailure",
            "InputValidation",
            "Validate",
            false,
            $"Input validation failed for field: {inputField}",
            new Dictionary<string, object>
            {
                ["InputField"] = inputField,
                ["ViolationType"] = violationType,
                ["SanitizedInput"] = sanitizedInput,
                ["InputLength"] = inputValue?.Length ?? 0
            },
            cancellationToken);
    }

    #endregion

    #region Data Security Logs

    public async Task LogSensitiveDataAccessAsync(string dataType, string operation, string? entityId = null, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "SensitiveDataAccess",
            dataType,
            operation,
            true,
            metadata: new Dictionary<string, object>
            {
                ["DataType"] = dataType,
                ["Operation"] = operation,
                ["EntityId"] = entityId ?? "unknown",
                ["AccessTime"] = DateTime.UtcNow
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogDataLeakageAttemptAsync(string dataType, string leakageMethod, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "DataLeakageAttempt",
            dataType,
            "DataExfiltration",
            false,
            $"Potential data leakage attempt via {leakageMethod}",
            new Dictionary<string, object>
            {
                ["DataType"] = dataType,
                ["LeakageMethod"] = leakageMethod,
                ["AttemptTime"] = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task LogEncryptionEventAsync(string operation, string dataType, bool success, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "EncryptionEvent",
            "Encryption",
            operation,
            success,
            success ? null : "Encryption operation failed",
            new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["DataType"] = dataType,
                ["Success"] = success
            },
            cancellationToken);
    }

    #endregion

    #region System Security Logs

    public async Task LogConfigurationChangeAsync(string configType, string oldValue, string newValue, CancellationToken cancellationToken = default)
    {
        // 機密設定値をマスク
        var maskedOldValue = MaskSensitiveConfigValue(configType, oldValue);
        var maskedNewValue = MaskSensitiveConfigValue(configType, newValue);

        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "ConfigurationChange",
            "SystemConfiguration",
            "Modify",
            true,
            metadata: new Dictionary<string, object>
            {
                ["ConfigType"] = configType,
                ["OldValue"] = maskedOldValue,
                ["NewValue"] = maskedNewValue,
                ["ChangeTime"] = DateTime.UtcNow
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogSecurityScanDetectedAsync(string scanType, string sourceIp, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "SecurityScanDetected",
            "SystemSecurity",
            "ScanDetection",
            false,
            $"Security scan detected from {sourceIp}",
            new Dictionary<string, object>
            {
                ["ScanType"] = scanType,
                ["SourceIp"] = sourceIp,
                ["DetectionTime"] = DateTime.UtcNow,
                ["UserAgent"] = GetUserAgent()
            },
            cancellationToken);
    }

    public async Task LogMaliciousRequestAsync(string requestType, string payload, CancellationToken cancellationToken = default)
    {
        var sanitizedPayload = SanitizeInputForLogging(payload);
        
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            "MaliciousRequest",
            "WebSecurity",
            "RequestAnalysis",
            false,
            "Malicious request detected",
            new Dictionary<string, object>
            {
                ["RequestType"] = requestType,
                ["SanitizedPayload"] = sanitizedPayload,
                ["PayloadLength"] = payload?.Length ?? 0,
                ["RequestPath"] = GetRequestPath(),
                ["Method"] = GetRequestMethod()
            },
            cancellationToken);
    }

    #endregion

    #region Helper Methods

    private string? GetRefererHeader()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.Referer.FirstOrDefault();
    }

    private string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();
    }

    private string? GetRequestPath()
    {
        return _httpContextAccessor.HttpContext?.Request.Path.Value;
    }

    private string? GetRequestMethod()
    {
        return _httpContextAccessor.HttpContext?.Request.Method;
    }

    private string SanitizeInputForLogging(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return "empty";

        // 長い入力を切り詰め
        const int maxLength = 500;
        var truncated = input.Length > maxLength ? input[..maxLength] + "..." : input;

        // 危険な文字をエスケープ
        return truncated
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\"", "\\\"")
            .Replace("'", "\\'");
    }

    private string MaskSensitiveConfigValue(string configType, string value)
    {
        var sensitiveConfigTypes = new[]
        {
            "password", "secret", "key", "token", "connectionstring",
            "apikey", "clientsecret", "privatekey"
        };

        if (sensitiveConfigTypes.Any(type => configType.Contains(type, StringComparison.OrdinalIgnoreCase)))
        {
            return "***MASKED***";
        }

        return value;
    }

    #endregion
}