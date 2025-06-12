using NUnit.Framework;
using TicketManagement.Core.Entities;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class CommentTests
{
    private Comment _comment;
    private Guid _ticketId;

    [SetUp]
    public void Setup()
    {
        _ticketId = Guid.NewGuid();
        _comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticketId,
            Content = "Test comment content",
            AuthorId = "test-user",
            CreatedAt = DateTime.UtcNow
        };
    }

    [Test]
    public void Comment_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var comment = new Comment();

        // Assert
        Assert.That(comment.Content, Is.EqualTo(string.Empty));
        Assert.That(comment.AuthorId, Is.EqualTo(string.Empty));
        Assert.That(comment.IsEdited, Is.False);
        Assert.That(comment.UpdatedAt, Is.Null);
    }

    [Test]
    public void Comment_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_comment.TicketId, Is.EqualTo(_ticketId));
        Assert.That(_comment.Content, Is.EqualTo("Test comment content"));
        Assert.That(_comment.AuthorId, Is.EqualTo("test-user"));
    }

    [Test]
    public void UpdateContent_DifferentContent_UpdatesContentAndMarksAsEdited()
    {
        // Arrange
        var newContent = "Updated comment content";
        var userId = "test-user";

        // Act
        _comment.UpdateContent(newContent, userId);

        // Assert
        Assert.That(_comment.Content, Is.EqualTo(newContent));
        Assert.That(_comment.IsEdited, Is.True);
        Assert.That(_comment.UpdatedAt, Is.Not.Null);
        Assert.That(_comment.UpdatedAt, Is.GreaterThan(_comment.CreatedAt));
    }

    [Test]
    public void UpdateContent_SameContent_DoesNotMarkAsEdited()
    {
        // Arrange
        var originalContent = _comment.Content;
        var userId = "test-user";

        // Act
        _comment.UpdateContent(originalContent, userId);

        // Assert
        Assert.That(_comment.Content, Is.EqualTo(originalContent));
        Assert.That(_comment.IsEdited, Is.False);
        Assert.That(_comment.UpdatedAt, Is.Null);
    }

    [Test]
    public void UpdateContent_DifferentUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var newContent = "Updated content";
        var differentUserId = "different-user";

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => 
            _comment.UpdateContent(newContent, differentUserId));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void UpdateContent_EmptyContent_ThrowsArgumentException(string content)
    {
        // Arrange
        var userId = "test-user";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _comment.UpdateContent(content, userId));
    }

    [Test]
    public void UpdateContent_NullContent_ThrowsArgumentException()
    {
        // Arrange
        var userId = "test-user";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _comment.UpdateContent(null!, userId));
    }

    [Test]
    public void CanEdit_SameUser_ReturnsTrue()
    {
        // Arrange
        var userId = "test-user";

        // Act
        var result = _comment.CanEdit(userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanEdit_DifferentUser_ReturnsFalse()
    {
        // Arrange
        var differentUserId = "different-user";

        // Act
        var result = _comment.CanEdit(differentUserId);

        // Assert
        Assert.That(result, Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CanEdit_EmptyUserId_ReturnsFalse(string userId)
    {
        // Act
        var result = _comment.CanEdit(userId);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanEdit_NullUserId_ReturnsFalse()
    {
        // Act
        var result = _comment.CanEdit(null!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Comment_CreatedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _comment.CreatedAt = expectedDate;

        // Assert
        Assert.That(_comment.CreatedAt, Is.EqualTo(expectedDate));
    }

    [Test]
    public void Comment_Navigation_TicketProperty()
    {
        // Arrange
        var ticket = new Ticket { Id = _ticketId };

        // Act
        _comment.Ticket = ticket;

        // Assert
        Assert.That(_comment.Ticket, Is.EqualTo(ticket));
        Assert.That(_comment.Ticket.Id, Is.EqualTo(_ticketId));
    }

    [Test]
    public void Comment_IsEdited_InitiallyFalse()
    {
        // Arrange & Act
        var comment = new Comment();

        // Assert
        Assert.That(comment.IsEdited, Is.False);
    }

    [Test]
    public void Comment_MultipleUpdates_MaintainsEditedStatus()
    {
        // Arrange
        var userId = "test-user";

        // Act
        _comment.UpdateContent("First update", userId);
        _comment.UpdateContent("Second update", userId);

        // Assert
        Assert.That(_comment.Content, Is.EqualTo("Second update"));
        Assert.That(_comment.IsEdited, Is.True);
        Assert.That(_comment.UpdatedAt, Is.Not.Null);
    }
}