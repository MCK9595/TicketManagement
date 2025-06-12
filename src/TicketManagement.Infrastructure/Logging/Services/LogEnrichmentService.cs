using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using TicketManagement.Infrastructure.Logging.Models;

namespace TicketManagement.Infrastructure.Logging.Services;

/// <summary>
/// ログエンリッチメントサービスのインターフェース
/// </summary>
public interface ILogEnrichmentService
{
    /// <summary>
    /// ログイベントにコンテキスト情報を追加
    /// </summary>
    void EnrichLogEvent(LogEvent logEvent);
    
    /// <summary>
    /// 現在のユーザーコンテキストを取得
    /// </summary>
    UserContext GetCurrentUserContext();
    
    /// <summary>
    /// 相関IDを取得または生成
    /// </summary>
    string GetOrCreateCorrelationId();
}

/// <summary>
/// ユーザーコンテキスト情報
/// </summary>
public class UserContext
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
}

/// <summary>
/// ログエンリッチメントサービスの実装
/// </summary>
public class LogEnrichmentService : ILogEnrichmentService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";

    public LogEnrichmentService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void EnrichLogEvent(LogEvent logEvent)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        var userContext = GetCurrentUserContext();
        
        // ユーザー情報の設定
        logEvent.UserId = userContext.UserId;
        logEvent.UserName = userContext.UserName;
        logEvent.IpAddress = userContext.IpAddress;
        logEvent.UserAgent = userContext.UserAgent;
        logEvent.SessionId = userContext.SessionId;
        
        // 相関IDの設定
        logEvent.CorrelationId = GetOrCreateCorrelationId();
    }

    public UserContext GetCurrentUserContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return new UserContext();
        }

        var user = context.User;
        var userContext = new UserContext
        {
            IpAddress = GetClientIpAddress(context),
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
            SessionId = GetSessionIdSafe(context)
        };

        if (user.Identity?.IsAuthenticated == true)
        {
            userContext.UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                                  user.FindFirst("sub")?.Value;
            
            userContext.UserName = user.FindFirst(ClaimTypes.Name)?.Value ??
                                   user.FindFirst("name")?.Value ??
                                   user.FindFirst("preferred_username")?.Value;
            
            userContext.Email = user.FindFirst(ClaimTypes.Email)?.Value ??
                                user.FindFirst("email")?.Value;
            
            userContext.Roles = user.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Union(user.FindAll("role").Select(c => c.Value));
        }

        return userContext;
    }

    public string GetOrCreateCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return Guid.NewGuid().ToString();
        }

        // ヘッダーから相関IDを取得
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue))
        {
            var correlationId = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                context.Items[CorrelationIdKey] = correlationId;
                return correlationId;
            }
        }

        // コンテキストから既存の相関IDを取得
        if (context.Items.TryGetValue(CorrelationIdKey, out var existingId) && existingId is string existing)
        {
            return existing;
        }

        // 新しい相関IDを生成
        var newCorrelationId = Guid.NewGuid().ToString();
        context.Items[CorrelationIdKey] = newCorrelationId;
        context.Response.Headers.Append(CorrelationIdHeader, newCorrelationId);
        
        return newCorrelationId;
    }

    private string? GetSessionIdSafe(HttpContext context)
    {
        try
        {
            // セッション機能が有効かどうかをチェック
            var sessionFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>();
            if (sessionFeature == null)
            {
                return null; // セッション機能が無効
            }

            // セッションが使用可能かチェック
            if (sessionFeature.Session == null)
            {
                return null; // セッションオブジェクトが無効
            }

            return sessionFeature.Session.Id;
        }
        catch (InvalidOperationException)
        {
            // セッションが設定されていない場合のエラーを安全に処理
            return null;
        }
        catch (Exception)
        {
            // その他の予期しないエラーも安全に処理
            return null;
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // X-Forwarded-For ヘッダーをチェック（プロキシ/ロードバランサー経由の場合）
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                return firstIp;
            }
        }

        // X-Real-IP ヘッダーをチェック
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // リモートIPアドレスを取得
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// ログエンリッチメント拡張メソッド
/// </summary>
public static class LogEnrichmentExtensions
{
    /// <summary>
    /// 自動エンリッチメント付きでセキュリティログを記録
    /// </summary>
    public static async Task LogSecurityEventAsync(
        this IStructuredLogger logger,
        ILogEnrichmentService enrichmentService,
        string eventType,
        string? resource = null,
        string? action = null,
        bool success = true,
        string? failureReason = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var logEvent = new SecurityLogEvent
        {
            EventType = eventType,
            Resource = resource,
            Action = action,
            Success = success,
            FailureReason = failureReason,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        enrichmentService.EnrichLogEvent(logEvent);
        await logger.LogSecurityEventAsync(logEvent, cancellationToken);
    }

    /// <summary>
    /// 自動エンリッチメント付きで監査ログを記録
    /// </summary>
    public static async Task LogAuditEventAsync(
        this IStructuredLogger logger,
        ILogEnrichmentService enrichmentService,
        string operation,
        string entityType,
        string? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        Dictionary<string, object>? changes = null,
        CancellationToken cancellationToken = default)
    {
        var logEvent = new AuditLogEvent
        {
            Operation = operation,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            Changes = changes ?? new Dictionary<string, object>()
        };

        enrichmentService.EnrichLogEvent(logEvent);
        await logger.LogAuditEventAsync(logEvent, cancellationToken);
    }

    /// <summary>
    /// 自動エンリッチメント付きでユーザーアクティビティログを記録
    /// </summary>
    public static async Task LogUserActivityAsync(
        this IStructuredLogger logger,
        ILogEnrichmentService enrichmentService,
        string activityType,
        string? resourceType = null,
        string? resourceId = null,
        string? description = null,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var logEvent = new UserActivityLogEvent
        {
            ActivityType = activityType,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Description = description,
            Parameters = parameters ?? new Dictionary<string, object>()
        };

        enrichmentService.EnrichLogEvent(logEvent);
        await logger.LogUserActivityAsync(logEvent, cancellationToken);
    }

    /// <summary>
    /// 自動エンリッチメント付きでビジネスログを記録
    /// </summary>
    public static async Task LogBusinessEventAsync(
        this IStructuredLogger logger,
        ILogEnrichmentService enrichmentService,
        string businessProcess,
        string action,
        string? entityType = null,
        string? entityId = null,
        bool success = true,
        string? errorMessage = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var logEvent = new BusinessLogEvent
        {
            BusinessProcess = businessProcess,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Success = success,
            ErrorMessage = errorMessage,
            Context = context ?? new Dictionary<string, object>()
        };

        enrichmentService.EnrichLogEvent(logEvent);
        await logger.LogBusinessEventAsync(logEvent, cancellationToken);
    }
}