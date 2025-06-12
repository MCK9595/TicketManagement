using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using TicketManagement.ApiService.Hubs;
using TicketManagement.ApiService.Services;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.SignalR;

[TestFixture]
public class SignalRNotificationServiceTests
{
    private Mock<IHubContext<NotificationHub>> _mockHubContext;
    private Mock<INotificationService> _mockNotificationService;
    private Mock<ILogger<SignalRNotificationService>> _mockLogger;
    private Mock<IHubClients> _mockClients;
    private Mock<IClientProxy> _mockClientProxy;
    private SignalRNotificationService _service;
    private string _testUserId;

    [SetUp]
    public void Setup()
    {
        _mockHubContext = new Mock<IHubContext<NotificationHub>>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<SignalRNotificationService>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _testUserId = "test-user-123";

        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _service = new SignalRNotificationService(
            _mockHubContext.Object,
            _mockNotificationService.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task SendNotificationToUserAsync_ValidNotification_SendsNotificationAndUpdatesUnreadCount()
    {
        // Arrange
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Test Notification",
            Message = "Test notification message",
            Type = NotificationType.TicketAssigned,
            RelatedTicketId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        var unreadCount = 5;
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(unreadCount);

        // Act
        await _service.SendNotificationToUserAsync(_testUserId, notification);

        // Assert
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveNotification", 
            It.Is<object[]>(args => 
                args.Length == 1 && 
                ((NotificationDto)args[0]).Id == notification.Id &&
                ((NotificationDto)args[0]).Title == notification.Title &&
                ((NotificationDto)args[0]).Message == notification.Message), 
            default), Times.Once);

        _mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => (int)args[0] == unreadCount), default), Times.Once);

        _mockNotificationService.Verify(s => s.GetUnreadCountAsync(_testUserId), Times.Once);
    }

    [Test]
    public async Task SendNotificationToUserAsync_ServiceThrowsException_LogsError()
    {
        // Arrange
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Test Notification",
            Message = "Test message",
            Type = NotificationType.StatusChanged,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        await _service.SendNotificationToUserAsync(_testUserId, notification);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error sending realtime notification to user {_testUserId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SendNotificationToUsersAsync_MultipleUsers_SendsToAllUsers()
    {
        // Arrange
        var userIds = new[] { "user1", "user2", "user3" };
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = "user1", // This will be overridden for each user
            Title = "Broadcast Notification",
            Message = "This is a broadcast message",
            Type = NotificationType.CommentAdded,
            CreatedAt = DateTime.UtcNow
        };

        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(It.IsAny<string>()))
            .ReturnsAsync(2);

        // Act
        await _service.SendNotificationToUsersAsync(userIds, notification);

        // Assert
        // Verify ReceiveNotification was sent to each user
        foreach (var userId in userIds)
        {
            _mockClients.Verify(c => c.Group($"user-{userId}"), Times.AtLeastOnce);
        }

        // Verify the correct number of calls to GetUnreadCountAsync
        _mockNotificationService.Verify(s => s.GetUnreadCountAsync(It.IsAny<string>()), Times.Exactly(3));
    }

    [Test]
    public async Task UpdateUnreadCountAsync_ValidUser_SendsUpdatedCount()
    {
        // Arrange
        var unreadCount = 7;
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(unreadCount);

        // Act
        await _service.UpdateUnreadCountAsync(_testUserId);

        // Assert
        _mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => (int)args[0] == unreadCount), default), Times.Once);
        _mockNotificationService.Verify(s => s.GetUnreadCountAsync(_testUserId), Times.Once);
    }

    [Test]
    public async Task UpdateUnreadCountAsync_ServiceThrowsException_LogsError()
    {
        // Arrange
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _service.UpdateUnreadCountAsync(_testUserId);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error updating unread count for user {_testUserId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SendNotificationToProjectMembersAsync_CurrentImplementation_LogsWarning()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Project Notification",
            Message = "Project related message",
            Type = NotificationType.MentionedInComment,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _service.SendNotificationToProjectMembersAsync(projectId, notification);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SendNotificationToProjectMembersAsync not fully implemented yet")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SendNotificationToUserAsync_AllNotificationTypes_HandlesCorrectly()
    {
        // Arrange
        var notificationTypes = Enum.GetValues<NotificationType>();
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(1);

        foreach (var type in notificationTypes)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                Title = $"Test {type}",
                Message = $"Test message for {type}",
                Type = type,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await _service.SendNotificationToUserAsync(_testUserId, notification);

            // Assert
            _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveNotification", 
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    ((NotificationDto)args[0]).Type == type), 
                default), Times.Once);
        }
    }

    [Test]
    public async Task SendNotificationToUserAsync_NotificationWithAllProperties_MapsCorrectly()
    {
        // Arrange
        var relatedTicketId = Guid.NewGuid();
        var readAt = DateTime.UtcNow.AddMinutes(-30);
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Complete Notification",
            Message = "This notification has all properties set",
            Type = NotificationType.CommentAdded,
            RelatedTicketId = relatedTicketId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-60),
            IsRead = true,
            ReadAt = readAt
        };

        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(0);

        // Act
        await _service.SendNotificationToUserAsync(_testUserId, notification);

        // Assert
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveNotification", 
            It.Is<object[]>(args => 
                args.Length == 1 && 
                ((NotificationDto)args[0]).Id == notification.Id &&
                ((NotificationDto)args[0]).UserId == notification.UserId &&
                ((NotificationDto)args[0]).Title == notification.Title &&
                ((NotificationDto)args[0]).Message == notification.Message &&
                ((NotificationDto)args[0]).Type == notification.Type &&
                ((NotificationDto)args[0]).RelatedTicketId == relatedTicketId &&
                ((NotificationDto)args[0]).IsRead == true &&
                ((NotificationDto)args[0]).ReadAt == readAt), 
            default), Times.Once);
    }
}