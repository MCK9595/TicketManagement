using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TicketManagement.Infrastructure.Services;

/// <summary>
/// Redisキャッシュサービス
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redisを使用したキャッシュサービスの実装
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private volatile bool _isCacheAvailable = true;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(5); // Retry after 5 minutes

    public CacheService(IDistributedCache distributedCache, ILogger<CacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    private bool ShouldRetryCache()
    {
        if (_isCacheAvailable) return true;
        
        if (DateTime.UtcNow - _lastFailureTime > _retryInterval)
        {
            _isCacheAvailable = true;
            _logger.LogInformation("Retrying cache operations after failure interval");
            return true;
        }
        
        return false;
    }

    private void HandleCacheFailure(Exception ex, string operation)
    {
        _isCacheAvailable = false;
        _lastFailureTime = DateTime.UtcNow;
        _logger.LogWarning(ex, "Cache operation '{Operation}' failed. Cache disabled for {RetryInterval} minutes", 
            operation, _retryInterval.TotalMinutes);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!ShouldRetryCache())
        {
            _logger.LogTrace("Cache unavailable, returning default for key: {Key}", key);
            return default;
        }

        try
        {
            var value = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogTrace("Cache miss for key: {Key}", key);
                return default;
            }

            var result = JsonSerializer.Deserialize<T>(value, _jsonOptions);
            _logger.LogTrace("Cache hit for key: {Key}", key);
            return result;
        }
        catch (Exception ex)
        {
            HandleCacheFailure(ex, "GetAsync");
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!ShouldRetryCache())
        {
            _logger.LogTrace("Cache unavailable, skipping set for key: {Key}", key);
            return;
        }

        try
        {
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
            
            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            else
            {
                // デフォルトで30分のキャッシュ
                options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            }

            await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
            _logger.LogTrace("Cache set for key: {Key}, expiration: {Expiration}", key, expiration ?? TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            HandleCacheFailure(ex, "SetAsync");
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!ShouldRetryCache())
        {
            _logger.LogTrace("Cache unavailable, skipping remove for key: {Key}", key);
            return;
        }

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
            _logger.LogTrace("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            HandleCacheFailure(ex, "RemoveAsync");
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // パターンによる削除は実装が複雑なため、個別のキーで管理することを推奨
        // ここでは基本的な実装のみ提供
        await Task.CompletedTask;
    }
}

/// <summary>
/// キャッシュキーの生成ヘルパー
/// </summary>
public static class CacheKeys
{
    public static string Project(Guid projectId) => $"project:{projectId}";
    public static string ProjectTickets(Guid projectId) => $"project:{projectId}:tickets";
    public static string ProjectMembers(Guid projectId) => $"project:{projectId}:members";
    public static string Ticket(Guid ticketId) => $"ticket:{ticketId}";
    public static string TicketComments(Guid ticketId) => $"ticket:{ticketId}:comments";
    public static string UserProjects(string userId) => $"user:{userId}:projects";
    public static string UserNotifications(string userId) => $"user:{userId}:notifications";
    public static string UserUnreadNotifications(string userId) => $"user:{userId}:unread_notifications";
}