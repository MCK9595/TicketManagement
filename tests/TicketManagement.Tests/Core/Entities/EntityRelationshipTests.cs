using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class EntityRelationshipTests
{
    private Project _project;
    private Ticket _ticket;
    private Comment _comment;
    private TicketAssignment _assignment;
    private TicketHistory _history;
    private ProjectMember _member;

    [SetUp]
    public void Setup()
    {
        _project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        _comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            Content = "Test comment",
            AuthorId = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        _assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            AssigneeId = "test-assignee",
            AssignedBy = "test-user",
            AssignedAt = DateTime.UtcNow
        };

        _history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            FieldName = "Status",
            OldValue = "Open",
            NewValue = "InProgress",
            ChangedBy = "test-user",
            ChangedAt = DateTime.UtcNow,
            ActionType = HistoryActionType.Updated
        };

        _member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            UserId = "test-user",
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };
    }

    [Test]
    public void Project_Ticket_OneToMany_Relationship()
    {
        // Act
        _project.Tickets.Add(_ticket);
        _ticket.Project = _project;

        // Assert
        Assert.That(_project.Tickets.Count, Is.EqualTo(1));
        Assert.That(_project.Tickets.First(), Is.EqualTo(_ticket));
        Assert.That(_ticket.Project, Is.EqualTo(_project));
        Assert.That(_ticket.ProjectId, Is.EqualTo(_project.Id));
    }

    [Test]
    public void Project_ProjectMember_OneToMany_Relationship()
    {
        // Act
        _project.Members.Add(_member);
        _member.Project = _project;

        // Assert
        Assert.That(_project.Members.Count, Is.EqualTo(1));
        Assert.That(_project.Members.First(), Is.EqualTo(_member));
        Assert.That(_member.Project, Is.EqualTo(_project));
        Assert.That(_member.ProjectId, Is.EqualTo(_project.Id));
    }

    [Test]
    public void Ticket_Comment_OneToMany_Relationship()
    {
        // Act
        _ticket.Comments.Add(_comment);
        _comment.Ticket = _ticket;

        // Assert
        Assert.That(_ticket.Comments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Comments.First(), Is.EqualTo(_comment));
        Assert.That(_comment.Ticket, Is.EqualTo(_ticket));
        Assert.That(_comment.TicketId, Is.EqualTo(_ticket.Id));
    }

    [Test]
    public void Ticket_TicketAssignment_OneToMany_Relationship()
    {
        // Act
        _ticket.Assignments.Add(_assignment);
        _assignment.Ticket = _ticket;

        // Assert
        Assert.That(_ticket.Assignments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Assignments.First(), Is.EqualTo(_assignment));
        Assert.That(_assignment.Ticket, Is.EqualTo(_ticket));
        Assert.That(_assignment.TicketId, Is.EqualTo(_ticket.Id));
    }

    [Test]
    public void Ticket_TicketHistory_OneToMany_Relationship()
    {
        // Act
        _ticket.Histories.Add(_history);
        _history.Ticket = _ticket;

        // Assert
        Assert.That(_ticket.Histories.Count, Is.EqualTo(1));
        Assert.That(_ticket.Histories.First(), Is.EqualTo(_history));
        Assert.That(_history.Ticket, Is.EqualTo(_ticket));
        Assert.That(_history.TicketId, Is.EqualTo(_ticket.Id));
    }

    [Test]
    public void Project_MultipleTickets_MaintainsCollection()
    {
        // Arrange
        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            Title = "Second Ticket",
            Description = "Second Description",
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _project.Tickets.Add(_ticket);
        _project.Tickets.Add(ticket2);

        // Assert
        Assert.That(_project.Tickets.Count, Is.EqualTo(2));
        Assert.That(_project.Tickets.Contains(_ticket), Is.True);
        Assert.That(_project.Tickets.Contains(ticket2), Is.True);
    }

    [Test]
    public void Ticket_MultipleComments_MaintainsCollection()
    {
        // Arrange
        var comment2 = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            Content = "Second comment",
            AuthorId = "test-user-2",
            CreatedAt = DateTime.UtcNow.AddMinutes(5)
        };

        // Act
        _ticket.Comments.Add(_comment);
        _ticket.Comments.Add(comment2);

        // Assert
        Assert.That(_ticket.Comments.Count, Is.EqualTo(2));
        Assert.That(_ticket.Comments.Contains(_comment), Is.True);
        Assert.That(_ticket.Comments.Contains(comment2), Is.True);
    }

    [Test]
    public void Ticket_MultipleAssignments_MaintainsCollection()
    {
        // Arrange
        var assignment2 = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            AssigneeId = "test-assignee-2",
            AssignedBy = "test-user",
            AssignedAt = DateTime.UtcNow.AddMinutes(10)
        };

        // Act
        _ticket.Assignments.Add(_assignment);
        _ticket.Assignments.Add(assignment2);

        // Assert
        Assert.That(_ticket.Assignments.Count, Is.EqualTo(2));
        Assert.That(_ticket.Assignments.Contains(_assignment), Is.True);
        Assert.That(_ticket.Assignments.Contains(assignment2), Is.True);
    }

    [Test]
    public void Ticket_HistoryTracking_MaintainsChronologicalOrder()
    {
        // Arrange
        var history2 = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket.Id,
            FieldName = "Priority",
            OldValue = "Medium",
            NewValue = "High",
            ChangedBy = "test-user",
            ChangedAt = DateTime.UtcNow.AddMinutes(5),
            ActionType = HistoryActionType.Updated
        };

        // Act
        _ticket.Histories.Add(_history);
        _ticket.Histories.Add(history2);

        // Assert
        Assert.That(_ticket.Histories.Count, Is.EqualTo(2));
        Assert.That(_ticket.Histories.Contains(_history), Is.True);
        Assert.That(_ticket.Histories.Contains(history2), Is.True);
        
        // Verify chronological order
        var orderedHistory = _ticket.Histories.OrderBy(h => h.ChangedAt).ToList();
        Assert.That(orderedHistory[0], Is.EqualTo(_history));
        Assert.That(orderedHistory[1], Is.EqualTo(history2));
    }

    [Test]
    public void Project_DifferentRoleMembers_MaintainsCollection()
    {
        // Arrange
        var adminMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            UserId = "admin-user",
            Role = ProjectRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        var viewerMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            UserId = "viewer-user",
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow.AddDays(1)
        };

        // Act
        _project.Members.Add(_member); // Regular member
        _project.Members.Add(adminMember);
        _project.Members.Add(viewerMember);

        // Assert
        Assert.That(_project.Members.Count, Is.EqualTo(3));
        Assert.That(_project.Members.Count(m => m.Role == ProjectRole.Member), Is.EqualTo(1));
        Assert.That(_project.Members.Count(m => m.Role == ProjectRole.Admin), Is.EqualTo(1));
        Assert.That(_project.Members.Count(m => m.Role == ProjectRole.Viewer), Is.EqualTo(1));
    }

    [Test]
    public void ComplexHierarchy_ProjectWithTicketsCommentsAndHistory()
    {
        // Arrange
        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            Title = "Complex Ticket",
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.Critical,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        var comment2 = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket2.Id,
            Content = "Complex comment",
            AuthorId = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _project.Tickets.Add(_ticket);
        _project.Tickets.Add(ticket2);
        _ticket.Comments.Add(_comment);
        ticket2.Comments.Add(comment2);
        _ticket.Histories.Add(_history);

        // Assert - Project level
        Assert.That(_project.Tickets.Count, Is.EqualTo(2));
        
        // Assert - Ticket level
        Assert.That(_ticket.Comments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Histories.Count, Is.EqualTo(1));
        Assert.That(ticket2.Comments.Count, Is.EqualTo(1));
        
        // Assert - Cross-entity relationships
        Assert.That(_comment.TicketId, Is.EqualTo(_ticket.Id));
        Assert.That(comment2.TicketId, Is.EqualTo(ticket2.Id));
        Assert.That(_history.TicketId, Is.EqualTo(_ticket.Id));
    }

    [Test]
    public void EntityIds_AreUniqueAcrossTypes()
    {
        // Assert
        var allIds = new[]
        {
            _project.Id,
            _ticket.Id,
            _comment.Id,
            _assignment.Id,
            _history.Id,
            _member.Id
        };

        Assert.That(allIds.Distinct().Count(), Is.EqualTo(allIds.Length));
    }
}