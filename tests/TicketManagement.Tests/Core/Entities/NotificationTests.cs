using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class NotificationTests
{
    private Notification _notification;

    [SetUp]
    public void Setup()
    {
        _notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = "test-user",
            Type = NotificationType.TicketAssigned,
            Title = "Test Notification",
            Message = "Test message content",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
    }

    [Test]
    public void Notification_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var notification = new Notification();

        // Assert
        Assert.That(notification.UserId, Is.EqualTo(string.Empty));
        Assert.That(notification.Title, Is.EqualTo(string.Empty));
        Assert.That(notification.Message, Is.EqualTo(string.Empty));
        Assert.That(notification.IsRead, Is.False);
        Assert.That(notification.Type, Is.EqualTo(NotificationType.TicketAssigned)); // Default enum value is 0
        Assert.That(notification.ReadAt, Is.Null);
    }

    [Test]
    public void Notification_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_notification.UserId, Is.EqualTo("test-user"));
        Assert.That(_notification.Type, Is.EqualTo(NotificationType.TicketAssigned));
        Assert.That(_notification.Title, Is.EqualTo("Test Notification"));
        Assert.That(_notification.Message, Is.EqualTo("Test message content"));
        Assert.That(_notification.IsRead, Is.False);
    }

    [Test]
    public void Notification_CreatedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _notification.CreatedAt = expectedDate;

        // Assert
        Assert.That(_notification.CreatedAt, Is.EqualTo(expectedDate));
    }

    [Test]
    public void Notification_ReadAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _notification.ReadAt = expectedDate;

        // Assert
        Assert.That(_notification.ReadAt, Is.EqualTo(expectedDate));
    }

    [TestCase(NotificationType.TicketAssigned)]
    [TestCase(NotificationType.CommentAdded)]
    [TestCase(NotificationType.StatusChanged)]
    [TestCase(NotificationType.MentionedInComment)]
    public void Notification_Type_CanBeSetToValidValues(NotificationType type)
    {
        // Act
        _notification.Type = type;

        // Assert
        Assert.That(_notification.Type, Is.EqualTo(type));
    }

    [Test]
    public void Notification_IsRead_CanBeToggled()
    {
        // Arrange
        Assert.That(_notification.IsRead, Is.False);

        // Act
        _notification.IsRead = true;

        // Assert
        Assert.That(_notification.IsRead, Is.True);
    }

    [Test]
    public void Notification_Id_IsUniqueGuid()
    {
        // Arrange
        var notification1 = new Notification { Id = Guid.NewGuid() };
        var notification2 = new Notification { Id = Guid.NewGuid() };

        // Assert
        Assert.That(notification1.Id, Is.Not.EqualTo(notification2.Id));
        Assert.That(notification1.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(notification2.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void Notification_Message_CanBeEmpty()
    {
        // Act
        _notification.Message = "";

        // Assert
        Assert.That(_notification.Message, Is.EqualTo(""));
    }

    [Test]
    public void Notification_Title_CanBeEmpty()
    {
        // Act
        _notification.Title = "";

        // Assert
        Assert.That(_notification.Title, Is.EqualTo(""));
    }

    [Test]
    public void Notification_ReadAt_InitiallyNull()
    {
        // Arrange & Act
        var notification = new Notification();

        // Assert
        Assert.That(notification.ReadAt, Is.Null);
    }
}