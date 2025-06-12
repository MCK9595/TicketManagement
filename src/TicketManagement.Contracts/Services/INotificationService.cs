using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;

namespace TicketManagement.Contracts.Services;

public interface INotificationService
{
    Task<Notification> CreateNotificationAsync(string userId, string title, string message, NotificationType type, Guid? relatedTicketId = null);
    Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId);
    Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task<PagedResult<Notification>> GetPagedNotificationsAsync(string userId, int page = 1, int pageSize = 20);
    Task SendRealtimeNotificationAsync(string userId, Notification notification);
}