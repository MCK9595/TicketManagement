using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Repositories;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<IEnumerable<Notification>> GetNotificationsByUserIdAsync(string userId);
    Task<IEnumerable<Notification>> GetUnreadNotificationsByUserIdAsync(string userId);
    Task<IEnumerable<Notification>> GetNotificationsByTypeAsync(string userId, NotificationType type);
    Task<int> GetUnreadCountByUserIdAsync(string userId);
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync(string userId);
    Task<PagedResult<Notification>> GetPagedNotificationsAsync(string userId, int page, int pageSize);
}