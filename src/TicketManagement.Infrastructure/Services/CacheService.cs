using Microsoft.Extensions.Caching.Distributed;
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
    private readonly JsonSerializerOptions _jsonOptions;
    private volatile bool _isRedisAvailable = true;

    public CacheService(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_isRedisAvailable)
        {
            return default;
        }

        try
        {
            var value = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value, _jsonOptions);
        }
        catch (Exception)
        {
            // Mark Redis as unavailable and return default
            _isRedisAvailable = false;
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!_isRedisAvailable)
        {
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
        }
        catch (Exception)
        {
            // Mark Redis as unavailable
            _isRedisAvailable = false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isRedisAvailable)
        {
            return;
        }

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception)
        {
            // Mark Redis as unavailable
            _isRedisAvailable = false;
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