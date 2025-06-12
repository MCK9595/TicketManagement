using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class TicketHistoryTests
{
    private TicketHistory _ticketHistory;
    private Guid _ticketId;

    [SetUp]
    public void Setup()
    {
        _ticketId = Guid.NewGuid();
        _ticketHistory = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = _ticketId,
            ChangedBy = "test-user",
            ChangedAt = DateTime.UtcNow,
            FieldName = "Status",
            OldValue = "Open",
            NewValue = "InProgress",
            ActionType = HistoryActionType.Updated
        };
    }

    [Test]
    public void TicketHistory_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var history = new TicketHistory();

        // Assert
        Assert.That(history.ChangedBy, Is.EqualTo(string.Empty));
        Assert.That(history.FieldName, Is.EqualTo(string.Empty));
        Assert.That(history.ActionType, Is.EqualTo(HistoryActionType.Created)); // Default enum value is 0 (Created)
        Assert.That(history.OldValue, Is.Null);
        Assert.That(history.NewValue, Is.Null);
    }

    [Test]
    public void TicketHistory_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_ticketHistory.TicketId, Is.EqualTo(_ticketId));
        Assert.That(_ticketHistory.ChangedBy, Is.EqualTo("test-user"));
        Assert.That(_ticketHistory.FieldName, Is.EqualTo("Status"));
        Assert.That(_ticketHistory.OldValue, Is.EqualTo("Open"));
        Assert.That(_ticketHistory.NewValue, Is.EqualTo("InProgress"));
        Assert.That(_ticketHistory.ActionType, Is.EqualTo(HistoryActionType.Updated));
    }

    [Test]
    public void TicketHistory_ChangedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _ticketHistory.ChangedAt = expectedDate;

        // Assert
        Assert.That(_ticketHistory.ChangedAt, Is.EqualTo(expectedDate));
    }

    [Test]
    public void TicketHistory_Navigation_TicketProperty()
    {
        // Arrange
        var ticket = new Ticket { Id = _ticketId, Title = "Test Ticket", CreatedBy = "creator", CreatedAt = DateTime.UtcNow };

        // Act
        _ticketHistory.Ticket = ticket;

        // Assert
        Assert.That(_ticketHistory.Ticket, Is.EqualTo(ticket));
        Assert.That(_ticketHistory.Ticket.Id, Is.EqualTo(_ticketId));
    }

    [TestCase(HistoryActionType.Created)]
    [TestCase(HistoryActionType.Updated)]
    [TestCase(HistoryActionType.Assigned)]
    [TestCase(HistoryActionType.Unassigned)]
    public void TicketHistory_ActionType_CanBeSetToValidValues(HistoryActionType actionType)
    {
        // Act
        _ticketHistory.ActionType = actionType;

        // Assert
        Assert.That(_ticketHistory.ActionType, Is.EqualTo(actionType));
    }

    [Test]
    public void TicketHistory_OldValue_CanBeNull()
    {
        // Act
        _ticketHistory.OldValue = null;

        // Assert
        Assert.That(_ticketHistory.OldValue, Is.Null);
    }

    [Test]
    public void TicketHistory_NewValue_CanBeNull()
    {
        // Act
        _ticketHistory.NewValue = null;

        // Assert
        Assert.That(_ticketHistory.NewValue, Is.Null);
    }

    [Test]
    public void TicketHistory_FieldName_CanBeSetToCommonFieldNames()
    {
        // Arrange
        var fieldNames = new[] { "Status", "Priority", "Title", "Description", "AssignedTo" };

        foreach (var fieldName in fieldNames)
        {
            // Act
            _ticketHistory.FieldName = fieldName;

            // Assert
            Assert.That(_ticketHistory.FieldName, Is.EqualTo(fieldName));
        }
    }

    [Test]
    public void TicketHistory_Id_IsUniqueGuid()
    {
        // Arrange
        var history1 = new TicketHistory { Id = Guid.NewGuid() };
        var history2 = new TicketHistory { Id = Guid.NewGuid() };

        // Assert
        Assert.That(history1.Id, Is.Not.EqualTo(history2.Id));
        Assert.That(history1.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(history2.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void TicketHistory_CompareEntries_ByChangedAt()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var history1 = new TicketHistory { ChangedAt = baseTime };
        var history2 = new TicketHistory { ChangedAt = baseTime.AddMinutes(1) };

        // Assert
        Assert.That(history1.ChangedAt, Is.LessThan(history2.ChangedAt));
    }
}