using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using TicketManagement.ApiService.Hubs;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using System.Security.Claims;

namespace TicketManagement.Tests.SignalR;

[TestFixture]
public class NotificationHubTests
{
    private Mock<INotificationService> _mockNotificationService;
    private Mock<ILogger<NotificationHub>> _mockLogger;
    private Mock<HubCallerContext> _mockContext;
    private Mock<IClientProxy> _mockClientProxy;
    private Mock<IHubCallerClients> _mockClients;
    private Mock<IGroupManager> _mockGroups;
    private NotificationHub _hub;
    private string _testUserId;

    [TearDown]
    public void TearDown()
    {
        _hub?.Dispose();
    }

    [SetUp]
    public void Setup()
    {
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<NotificationHub>>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _testUserId = "test-user-123";

        // Setup user claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId),
            new Claim("sub", _testUserId)
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        _mockContext.Setup(c => c.User).Returns(principal);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-123");

        var mockSingleClientProxy = _mockClientProxy.As<ISingleClientProxy>();
        _mockClients.Setup(c => c.Caller).Returns(mockSingleClientProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _hub = new NotificationHub(_mockNotificationService.Object, _mockLogger.Object)
        {
            Context = _mockContext.Object,
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object
        };
    }

    [Test]
    public async Task OnConnectedAsync_ValidUser_AddsToGroupAndSendsUnreadCount()
    {
        // Arrange
        var unreadCount = 5;
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(unreadCount);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockGroups.Verify(g => g.AddToGroupAsync("test-connection-123", $"user-{_testUserId}", default), Times.Once);
        _mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => (int)args[0] == unreadCount), default), Times.Once);
    }

    [Test]
    public async Task OnDisconnectedAsync_ValidUser_RemovesFromGroup()
    {
        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockGroups.Verify(g => g.RemoveFromGroupAsync("test-connection-123", $"user-{_testUserId}", default), Times.Once);
    }

    [Test]
    public async Task GetUnreadCount_ValidUser_ReturnsCount()
    {
        // Arrange
        var expectedCount = 3;
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _hub.GetUnreadCount();

        // Assert
        Assert.That(result, Is.EqualTo(expectedCount));
        _mockNotificationService.Verify(s => s.GetUnreadCountAsync(_testUserId), Times.Once);
    }

    [Test]
    public async Task MarkAsRead_ValidNotification_MarksAsReadAndUpdatesCount()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new Notification { Id = notificationId, UserId = _testUserId }
        };
        var newUnreadCount = 2;

        _mockNotificationService.Setup(s => s.GetUserNotificationsAsync(_testUserId))
            .ReturnsAsync(notifications);
        _mockNotificationService.Setup(s => s.GetUnreadCountAsync(_testUserId))
            .ReturnsAsync(newUnreadCount);

        // Act
        await _hub.MarkAsRead(notificationId);

        // Assert
        _mockNotificationService.Verify(s => s.MarkAsReadAsync(notificationId), Times.Once);
        _mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => (int)args[0] == newUnreadCount), default), Times.Once);
    }

    [Test]
    public async Task MarkAsRead_NotificationNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notifications = new List<Notification>(); // Empty list

        _mockNotificationService.Setup(s => s.GetUserNotificationsAsync(_testUserId))
            .ReturnsAsync(notifications);

        // Act & Assert
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _hub.MarkAsRead(notificationId));
    }

    [Test]
    public async Task MarkAllAsRead_ValidUser_MarksAllAsReadAndSendsZeroCount()
    {
        // Act
        await _hub.MarkAllAsRead();

        // Assert
        _mockNotificationService.Verify(s => s.MarkAllAsReadAsync(_testUserId), Times.Once);
        _mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => (int)args[0] == 0), default), Times.Once);
    }

    [Test]
    public void IsUserConnected_StaticMethod_WorksCorrectly()
    {
        // Act & Assert - Initially no users connected
        Assert.That(NotificationHub.IsUserConnected(_testUserId), Is.False);
        
        // Note: Testing static methods that modify static state is challenging
        // In a real implementation, we might want to refactor these to be instance methods
        // or provide a way to reset the static state for testing
    }

    [Test]
    public void GetConnectedUserIds_StaticMethod_ReturnsEmptyInitially()
    {
        // Act
        var connectedUsers = NotificationHub.GetConnectedUserIds();

        // Assert
        Assert.That(connectedUsers, Is.Empty);
    }

    [Test]
    public async Task SendNotificationToUser_StaticMethod_SendsNotificationAndUnreadCount()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Title = "Test Notification",
            Message = "Test Message",
            Type = NotificationType.TicketAssigned
        };

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group($"user-{_testUserId}")).Returns(mockClientProxy.Object);

        // Act
        await NotificationHub.SendNotificationToUser(mockHubContext.Object, _testUserId, notification);

        // Assert
        mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveNotification", 
            It.Is<object[]>(args => args[0] == notification), default), Times.Once);
        mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => args[0] == notification), default), Times.Once);
    }

    [Test]
    public async Task SendNotificationToUsers_StaticMethod_SendsToMultipleUsers()
    {
        // Arrange
        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        
        var userIds = new[] { "user1", "user2", "user3" };
        var notification = new NotificationDto
        {
            Id = Guid.NewGuid(),
            Title = "Test Notification",
            Message = "Test Message",
            Type = NotificationType.CommentAdded
        };

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        // Act
        await NotificationHub.SendNotificationToUsers(mockHubContext.Object, userIds, notification);

        // Assert
        // Verify that SendCoreAsync was called for each user (twice per user: ReceiveNotification + UpdateUnreadCount)
        mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveNotification", 
            It.Is<object[]>(args => args[0] == notification), default), Times.Exactly(3));
        mockClientProxy.Verify(c => c.SendCoreAsync("UpdateUnreadCount", 
            It.Is<object[]>(args => args[0] == notification), default), Times.Exactly(3));
    }

    [Test]
    public void GetCurrentUserId_NoUserContext_ThrowsUnauthorizedException()
    {
        // Arrange
        var mockContextWithoutUser = new Mock<HubCallerContext>();
        mockContextWithoutUser.Setup(c => c.User).Returns((ClaimsPrincipal)null!);
        
        var hubWithoutUser = new NotificationHub(_mockNotificationService.Object, _mockLogger.Object)
        {
            Context = mockContextWithoutUser.Object
        };

        // Act & Assert
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => hubWithoutUser.GetUnreadCount());
    }

    [Test]
    public void GetCurrentUserId_NoUserIdClaim_ThrowsUnauthorizedException()
    {
        // Arrange
        var emptyIdentity = new ClaimsIdentity();
        var principalWithoutUserId = new ClaimsPrincipal(emptyIdentity);
        
        var mockContextWithoutUserId = new Mock<HubCallerContext>();
        mockContextWithoutUserId.Setup(c => c.User).Returns(principalWithoutUserId);
        
        var hubWithoutUserId = new NotificationHub(_mockNotificationService.Object, _mockLogger.Object)
        {
            Context = mockContextWithoutUserId.Object
        };

        // Act & Assert
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => hubWithoutUserId.GetUnreadCount());
    }
}