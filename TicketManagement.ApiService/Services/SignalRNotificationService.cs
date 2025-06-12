using Microsoft.AspNetCore.SignalR;
using TicketManagement.ApiService.Hubs;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;

namespace TicketManagement.ApiService.Services;

/// <summary>
/// SignalR経由でリアルタイム通知を送信するサービス
/// </summary>
public class SignalRNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hubContext,
        INotificationService notificationService,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// 特定のユーザーにリアルタイム通知を送信
    /// </summary>
    public async Task SendNotificationToUserAsync(string userId, Notification notification)
    {
        try
        {
            var notificationDto = new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                RelatedTicketId = notification.RelatedTicketId,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                RelatedTicketTitle = null // TODO: チケットタイトルを取得する実装が必要
            };

            await _hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", notificationDto);
            
            // 未読数も更新
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            await _hubContext.Clients.Group($"user-{userId}").SendAsync("UpdateUnreadCount", unreadCount);
            
            _logger.LogInformation("Sent realtime notification to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending realtime notification to user {UserId}", userId);
        }
    }

    /// <summary>
    /// 複数のユーザーにリアルタイム通知を送信
    /// </summary>
    public async Task SendNotificationToUsersAsync(IEnumerable<string> userIds, Notification notification)
    {
        var tasks = userIds.Select(userId => SendNotificationToUserAsync(userId, notification));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// プロジェクトの全メンバーに通知を送信
    /// </summary>
    public async Task SendNotificationToProjectMembersAsync(Guid projectId, Notification notification)
    {
        try
        {
            // プロジェクトメンバーのIDを取得する実装が必要
            _logger.LogWarning("SendNotificationToProjectMembersAsync not fully implemented yet");
            await Task.CompletedTask; // 警告を解消するため
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to project members {ProjectId}", projectId);
        }
    }

    /// <summary>
    /// 未読数を更新
    /// </summary>
    public async Task UpdateUnreadCountAsync(string userId)
    {
        try
        {
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            await _hubContext.Clients.Group($"user-{userId}").SendAsync("UpdateUnreadCount", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unread count for user {UserId}", userId);
        }
    }
}