using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class TicketTests
{
    private Ticket _ticket;
    private Guid _projectId;

    [SetUp]
    public void Setup()
    {
        _projectId = Guid.NewGuid();
        _ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };
    }

    [Test]
    public void Ticket_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var ticket = new Ticket();

        // Assert
        Assert.That(ticket.Title, Is.EqualTo(string.Empty));
        Assert.That(ticket.Description, Is.EqualTo(string.Empty));
        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.Open));
        Assert.That(ticket.Priority, Is.EqualTo(TicketPriority.Low)); // Default enum value is 0 (Low)
        Assert.That(ticket.Category, Is.EqualTo(string.Empty));
        Assert.That(ticket.Tags, Is.Not.Null);
        Assert.That(ticket.Tags.Length, Is.EqualTo(0));
        Assert.That(ticket.Comments, Is.Not.Null);
        Assert.That(ticket.Assignments, Is.Not.Null);
        Assert.That(ticket.Histories, Is.Not.Null);
    }

    [Test]
    public void UpdateStatus_ValidTransition_UpdatesStatusAndAddsHistory()
    {
        // Arrange
        var userId = "test-user";
        var initialHistoryCount = _ticket.Histories.Count;

        // Act
        _ticket.UpdateStatus(TicketStatus.InProgress, userId);

        // Assert
        Assert.That(_ticket.Status, Is.EqualTo(TicketStatus.InProgress));
        Assert.That(_ticket.UpdatedAt, Is.Not.Null);
        Assert.That(_ticket.UpdatedBy, Is.EqualTo(userId));
        Assert.That(_ticket.Histories.Count, Is.EqualTo(initialHistoryCount + 1));
        
        var historyEntry = _ticket.Histories.Last();
        Assert.That(historyEntry.FieldName, Is.EqualTo("Status"));
        Assert.That(historyEntry.OldValue, Is.EqualTo(TicketStatus.Open.ToString()));
        Assert.That(historyEntry.NewValue, Is.EqualTo(TicketStatus.InProgress.ToString()));
        Assert.That(historyEntry.ChangedBy, Is.EqualTo(userId));
        Assert.That(historyEntry.ActionType, Is.EqualTo(HistoryActionType.Updated));
    }

    [Test]
    public void UpdateStatus_SameStatus_DoesNotAddHistory()
    {
        // Arrange
        var userId = "test-user";
        var initialHistoryCount = _ticket.Histories.Count;

        // Act
        _ticket.UpdateStatus(TicketStatus.Open, userId);

        // Assert
        Assert.That(_ticket.Status, Is.EqualTo(TicketStatus.Open));
        Assert.That(_ticket.Histories.Count, Is.EqualTo(initialHistoryCount));
    }

    [TestCase(TicketStatus.Open, TicketStatus.InProgress, true)]
    [TestCase(TicketStatus.InProgress, TicketStatus.Review, true)]
    [TestCase(TicketStatus.Review, TicketStatus.Closed, true)]
    [TestCase(TicketStatus.Open, TicketStatus.OnHold, true)]
    [TestCase(TicketStatus.InProgress, TicketStatus.Open, false)] // Cannot go back from InProgress to Open
    [TestCase(TicketStatus.Closed, TicketStatus.Open, true)] // Can reopen closed tickets
    [TestCase(TicketStatus.OnHold, TicketStatus.InProgress, true)]
    public void CanTransitionTo_ValidatesStatusTransitions(TicketStatus from, TicketStatus to, bool expected)
    {
        // Arrange
        _ticket.Status = from;

        // Act
        var result = _ticket.CanTransitionTo(to);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void UpdatePriority_DifferentPriority_UpdatesAndAddsHistory()
    {
        // Arrange
        var userId = "test-user";
        var newPriority = TicketPriority.High;
        var initialHistoryCount = _ticket.Histories.Count;

        // Act
        _ticket.UpdatePriority(newPriority, userId);

        // Assert
        Assert.That(_ticket.Priority, Is.EqualTo(newPriority));
        Assert.That(_ticket.UpdatedAt, Is.Not.Null);
        Assert.That(_ticket.UpdatedBy, Is.EqualTo(userId));
        Assert.That(_ticket.Histories.Count, Is.EqualTo(initialHistoryCount + 1));
        
        var historyEntry = _ticket.Histories.Last();
        Assert.That(historyEntry.FieldName, Is.EqualTo("Priority"));
        Assert.That(historyEntry.OldValue, Is.EqualTo(TicketPriority.Medium.ToString()));
        Assert.That(historyEntry.NewValue, Is.EqualTo(TicketPriority.High.ToString()));
    }

    [Test]
    public void UpdatePriority_SamePriority_DoesNotAddHistory()
    {
        // Arrange
        var userId = "test-user";
        var initialHistoryCount = _ticket.Histories.Count;

        // Act
        _ticket.UpdatePriority(TicketPriority.Medium, userId);

        // Assert
        Assert.That(_ticket.Priority, Is.EqualTo(TicketPriority.Medium));
        Assert.That(_ticket.Histories.Count, Is.EqualTo(initialHistoryCount));
    }

    [Test]
    public void AddComment_AddsToCommentsCollection()
    {
        // Arrange
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            Content = "Test comment",
            AuthorId = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _ticket.Comments.Add(comment);

        // Assert
        Assert.That(_ticket.Comments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Comments.First(), Is.EqualTo(comment));
    }

    [Test]
    public void AddAssignment_AddsToAssignmentsCollection()
    {
        // Arrange
        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            AssigneeId = "test-assignee",
            AssignedBy = "test-user",
            AssignedAt = DateTime.UtcNow
        };

        // Act
        _ticket.Assignments.Add(assignment);

        // Assert
        Assert.That(_ticket.Assignments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Assignments.First(), Is.EqualTo(assignment));
    }

    [Test]
    public void Ticket_Tags_CanBeSetAndRetrieved()
    {
        // Arrange
        var tags = new[] { "bug", "high-priority", "ui" };

        // Act
        _ticket.Tags = tags;

        // Assert
        Assert.That(_ticket.Tags, Is.EqualTo(tags));
        Assert.That(_ticket.Tags.Length, Is.EqualTo(3));
    }

    [Test]
    public void Ticket_DueDate_CanBeSetAndCleared()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(7);

        // Act
        _ticket.DueDate = dueDate;

        // Assert
        Assert.That(_ticket.DueDate, Is.EqualTo(dueDate));

        // Act - Clear due date
        _ticket.DueDate = null;

        // Assert
        Assert.That(_ticket.DueDate, Is.Null);
    }

    [Test]
    public void Ticket_Category_CanBeSetAndRetrieved()
    {
        // Arrange
        var category = "Bug Fix";

        // Act
        _ticket.Category = category;

        // Assert
        Assert.That(_ticket.Category, Is.EqualTo(category));
    }
}