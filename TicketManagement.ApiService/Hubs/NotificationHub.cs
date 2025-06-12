using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;

namespace TicketManagement.ApiService.Hubs;

/// <summary>
/// リアルタイム通知用のSignalR Hub
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;

    // ユーザーIDとConnectionIDのマッピングを管理
    private static readonly Dictionary<string, HashSet<string>> UserConnections = new();
    private static readonly object ConnectionLock = new();

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               Context.User?.FindFirst("sub")?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// クライアント接続時の処理
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            lock (ConnectionLock)
            {
                if (!UserConnections.ContainsKey(userId))
                {
                    UserConnections[userId] = new HashSet<string>();
                }
                UserConnections[userId].Add(Context.ConnectionId);
            }

            // ユーザーグループに追加
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            // 未読通知数を送信
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UpdateUnreadCount", unreadCount);

            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
            throw;
        }
    }

    /// <summary>
    /// クライアント切断時の処理
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            lock (ConnectionLock)
            {
                if (UserConnections.ContainsKey(userId))
                {
                    UserConnections[userId].Remove(Context.ConnectionId);
                    
                    if (UserConnections[userId].Count == 0)
                    {
                        UserConnections.Remove(userId);
                    }
                }
            }

            // ユーザーグループから削除
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            _logger.LogInformation("User {UserId} disconnected with connection {ConnectionId}", userId, Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
        }
    }

    /// <summary>
    /// 未読通知数を取得
    /// </summary>
    public async Task<int> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            return await _notificationService.GetUnreadCountAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            throw;
        }
    }

    /// <summary>
    /// 通知を既読にマーク
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // 通知がユーザーのものかチェック
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            if (!notifications.Any(n => n.Id == notificationId))
            {
                throw new UnauthorizedAccessException("Notification not found or access denied");
            }

            await _notificationService.MarkAsReadAsync(notificationId);
            
            // 更新された未読数を送信
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UpdateUnreadCount", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            throw;
        }
    }

    /// <summary>
    /// すべての通知を既読にマーク
    /// </summary>
    public async Task MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _notificationService.MarkAllAsReadAsync(userId);
            
            // 未読数を0に更新
            await Clients.Caller.SendAsync("UpdateUnreadCount", 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            throw;
        }
    }

    /// <summary>
    /// 特定のユーザーに通知を送信（サーバー側から呼び出し用）
    /// </summary>
    public static async Task SendNotificationToUser(
        IHubContext<NotificationHub> hubContext, 
        string userId, 
        NotificationDto notification)
    {
        await hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", notification);
        
        // 未読数も更新
        await hubContext.Clients.Group($"user-{userId}").SendAsync("UpdateUnreadCount", notification);
    }

    /// <summary>
    /// 複数のユーザーに通知を送信（サーバー側から呼び出し用）
    /// </summary>
    public static async Task SendNotificationToUsers(
        IHubContext<NotificationHub> hubContext, 
        IEnumerable<string> userIds, 
        NotificationDto notification)
    {
        var tasks = userIds.Select(userId => 
            SendNotificationToUser(hubContext, userId, notification));
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 接続中のユーザーIDリストを取得（管理用）
    /// </summary>
    public static IEnumerable<string> GetConnectedUserIds()
    {
        lock (ConnectionLock)
        {
            return UserConnections.Keys.ToList();
        }
    }

    /// <summary>
    /// 特定ユーザーが接続中かチェック
    /// </summary>
    public static bool IsUserConnected(string userId)
    {
        lock (ConnectionLock)
        {
            return UserConnections.ContainsKey(userId);
        }
    }
}