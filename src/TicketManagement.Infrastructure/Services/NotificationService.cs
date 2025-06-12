using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<Notification> CreateNotificationAsync(
        string userId, 
        string title, 
        string message, 
        NotificationType type, 
        Guid? relatedTicketId = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedTicketId = relatedTicketId,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        var createdNotification = await _notificationRepository.AddAsync(notification);

        // リアルタイム通知の送信（SignalR実装時）
        await SendRealtimeNotificationAsync(userId, createdNotification);

        return createdNotification;
    }

    public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId)
    {
        return await _notificationRepository.GetNotificationsByUserIdAsync(userId);
    }

    public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId)
    {
        return await _notificationRepository.GetUnreadNotificationsByUserIdAsync(userId);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _notificationRepository.GetUnreadCountByUserIdAsync(userId);
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        await _notificationRepository.MarkAsReadAsync(notificationId);
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _notificationRepository.MarkAllAsReadAsync(userId);
    }

    public async Task<PagedResult<Notification>> GetPagedNotificationsAsync(string userId, int page = 1, int pageSize = 20)
    {
        return await _notificationRepository.GetPagedNotificationsAsync(userId, page, pageSize);
    }

    public async Task SendRealtimeNotificationAsync(string userId, Notification notification)
    {
        // TODO: SignalR Hub実装時にリアルタイム通知を送信
        // 現在は空実装
        await Task.CompletedTask;
    }
}