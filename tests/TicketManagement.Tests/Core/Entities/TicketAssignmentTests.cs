using NUnit.Framework;
using TicketManagement.Core.Entities;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class TicketAssignmentTests
{
    private TicketAssignment _ticketAssignment;
    private Guid _ticketId;

    [SetUp]
    public void Setup()
    {
        _ticketId = Guid.NewGuid();
        _ticketAssignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticketId,
            AssigneeId = "test-assignee",
            AssignedBy = "test-assigner",
            AssignedAt = DateTime.UtcNow
        };
    }

    [Test]
    public void TicketAssignment_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var assignment = new TicketAssignment();

        // Assert
        Assert.That(assignment.AssigneeId, Is.EqualTo(string.Empty));
        Assert.That(assignment.AssignedBy, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TicketAssignment_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_ticketAssignment.TicketId, Is.EqualTo(_ticketId));
        Assert.That(_ticketAssignment.AssigneeId, Is.EqualTo("test-assignee"));
        Assert.That(_ticketAssignment.AssignedBy, Is.EqualTo("test-assigner"));
    }

    [Test]
    public void TicketAssignment_AssignedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _ticketAssignment.AssignedAt = expectedDate;

        // Assert
        Assert.That(_ticketAssignment.AssignedAt, Is.EqualTo(expectedDate));
    }

    [Test]
    public void TicketAssignment_Navigation_TicketProperty()
    {
        // Arrange
        var ticket = new Ticket { Id = _ticketId, Title = "Test Ticket", CreatedBy = "creator", CreatedAt = DateTime.UtcNow };

        // Act
        _ticketAssignment.Ticket = ticket;

        // Assert
        Assert.That(_ticketAssignment.Ticket, Is.EqualTo(ticket));
        Assert.That(_ticketAssignment.Ticket.Id, Is.EqualTo(_ticketId));
    }

    [Test]
    public void TicketAssignment_Id_IsUniqueGuid()
    {
        // Arrange
        var assignment1 = new TicketAssignment { Id = Guid.NewGuid() };
        var assignment2 = new TicketAssignment { Id = Guid.NewGuid() };

        // Assert
        Assert.That(assignment1.Id, Is.Not.EqualTo(assignment2.Id));
        Assert.That(assignment1.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(assignment2.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void TicketAssignment_AssigneeId_CanBeEmpty()
    {
        // Act
        _ticketAssignment.AssigneeId = "";

        // Assert
        Assert.That(_ticketAssignment.AssigneeId, Is.EqualTo(""));
    }

    [Test]
    public void TicketAssignment_AssignedBy_CanBeEmpty()
    {
        // Act
        _ticketAssignment.AssignedBy = "";

        // Assert
        Assert.That(_ticketAssignment.AssignedBy, Is.EqualTo(""));
    }

    [Test]
    public void TicketAssignment_CompareAssignments_ByAssignedAt()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var assignment1 = new TicketAssignment { AssignedAt = baseTime };
        var assignment2 = new TicketAssignment { AssignedAt = baseTime.AddMinutes(1) };

        // Assert
        Assert.That(assignment1.AssignedAt, Is.LessThan(assignment2.AssignedAt));
    }
}