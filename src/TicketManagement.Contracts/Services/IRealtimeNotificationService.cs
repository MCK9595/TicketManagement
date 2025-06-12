using TicketManagement.Core.Entities;

namespace TicketManagement.Contracts.Services;

/// <summary>
/// リアルタイム通知送信サービスのインターフェース
/// </summary>
public interface IRealtimeNotificationService
{
    Task SendNotificationToUserAsync(string userId, Notification notification);
    Task SendNotificationToUsersAsync(IEnumerable<string> userIds, Notification notification);
    Task SendNotificationToProjectMembersAsync(Guid projectId, Notification notification);
    Task UpdateUnreadCountAsync(string userId);
}