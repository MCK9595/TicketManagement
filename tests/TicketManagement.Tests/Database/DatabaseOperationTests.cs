using Microsoft.EntityFrameworkCore;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Database;

[TestFixture]
public class DatabaseOperationTests
{
    private TicketDbContext _context;
    private DbContextOptions<TicketDbContext> _options;

    [SetUp]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TicketDbContext(_options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task CreateProject_SavesCorrectlyToDatabase()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Act
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        // Assert
        var savedProject = await _context.Projects.FindAsync(project.Id);
        Assert.That(savedProject, Is.Not.Null);
        Assert.That(savedProject.Name, Is.EqualTo("Test Project"));
        Assert.That(savedProject.Description, Is.EqualTo("Test Description"));
        Assert.That(savedProject.CreatedBy, Is.EqualTo("test-user"));
        Assert.That(savedProject.IsActive, Is.True);
    }

    [Test]
    public async Task CreateTicket_SavesCorrectlyToDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Assert
        var savedTicket = await _context.Tickets
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == ticket.Id);

        Assert.That(savedTicket, Is.Not.Null);
        Assert.That(savedTicket.Title, Is.EqualTo("Test Ticket"));
        Assert.That(savedTicket.ProjectId, Is.EqualTo(projectId));
        Assert.That(savedTicket.Project, Is.Not.Null);
        Assert.That(savedTicket.Project.Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task CreateComment_SavesCorrectlyToDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Test Ticket",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Content = "Test comment content",
            AuthorId = "comment-author",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Assert
        var savedComment = await _context.Comments
            .Include(c => c.Ticket)
            .FirstOrDefaultAsync(c => c.Id == comment.Id);

        Assert.That(savedComment, Is.Not.Null);
        Assert.That(savedComment.Content, Is.EqualTo("Test comment content"));
        Assert.That(savedComment.AuthorId, Is.EqualTo("comment-author"));
        Assert.That(savedComment.Ticket, Is.Not.Null);
        Assert.That(savedComment.Ticket.Title, Is.EqualTo("Test Ticket"));
    }

    [Test]
    public async Task UpdateTicket_UpdatesCorrectlyInDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Original Title",
            Description = "Original Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Low,
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        ticket.Title = "Updated Title";
        ticket.Description = "Updated Description";
        ticket.Priority = TicketPriority.High;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = "updater";

        await _context.SaveChangesAsync();

        // Assert
        var updatedTicket = await _context.Tickets.FindAsync(ticket.Id);
        Assert.That(updatedTicket, Is.Not.Null);
        Assert.That(updatedTicket.Title, Is.EqualTo("Updated Title"));
        Assert.That(updatedTicket.Description, Is.EqualTo("Updated Description"));
        Assert.That(updatedTicket.Priority, Is.EqualTo(TicketPriority.High));
        Assert.That(updatedTicket.UpdatedBy, Is.EqualTo("updater"));
        Assert.That(updatedTicket.UpdatedAt, Is.Not.Null);
    }

    [Test]
    public async Task DeleteTicket_RemovesFromDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Ticket to Delete",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        _context.Tickets.Remove(ticket);
        await _context.SaveChangesAsync();

        // Assert
        var deletedTicket = await _context.Tickets.FindAsync(ticket.Id);
        Assert.That(deletedTicket, Is.Null);
    }

    [Test]
    public async Task ProjectWithMembers_SavesCorrectlyToDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Project with Members",
            CreatedBy = "project-owner",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var members = new[]
        {
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = "member1",
                Role = ProjectRole.Admin,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = "member2",
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            }
        };

        // Act
        _context.Projects.Add(project);
        _context.ProjectMembers.AddRange(members);
        await _context.SaveChangesAsync();

        // Assert
        var savedProject = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        Assert.That(savedProject, Is.Not.Null);
        Assert.That(savedProject.Members.Count, Is.EqualTo(2));
        Assert.That(savedProject.Members.Any(m => m.UserId == "member1" && m.Role == ProjectRole.Admin), Is.True);
        Assert.That(savedProject.Members.Any(m => m.UserId == "member2" && m.Role == ProjectRole.Member), Is.True);
    }

    [Test]
    public async Task TicketWithAssignments_SavesCorrectlyToDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Assigned Ticket",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssigneeId = "assignee1",
            AssignedBy = "assigner",
            AssignedAt = DateTime.UtcNow
        };

        // Act
        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        _context.TicketAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        // Assert
        var savedTicket = await _context.Tickets
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        Assert.That(savedTicket, Is.Not.Null);
        Assert.That(savedTicket.Assignments.Count, Is.EqualTo(1));
        
        var savedAssignment = savedTicket.Assignments.First();
        Assert.That(savedAssignment.AssigneeId, Is.EqualTo("assignee1"));
        Assert.That(savedAssignment.AssignedBy, Is.EqualTo("assigner"));
    }

    [Test]
    public async Task TicketWithHistory_SavesCorrectlyToDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Ticket with History",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        var historyEntries = new[]
        {
            new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                FieldName = "Status",
                OldValue = "Open",
                NewValue = "InProgress",
                ChangedBy = "user1",
                ChangedAt = DateTime.UtcNow,
                ActionType = HistoryActionType.Updated
            },
            new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                FieldName = "Priority",
                OldValue = "Low",
                NewValue = "High",
                ChangedBy = "user2",
                ChangedAt = DateTime.UtcNow.AddMinutes(5),
                ActionType = HistoryActionType.Updated
            }
        };

        // Act
        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        _context.TicketHistories.AddRange(historyEntries);
        await _context.SaveChangesAsync();

        // Assert
        var savedTicket = await _context.Tickets
            .Include(t => t.Histories)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        Assert.That(savedTicket, Is.Not.Null);
        Assert.That(savedTicket.Histories.Count, Is.EqualTo(2));
        
        Assert.That(savedTicket.Histories.Any(h => h.FieldName == "Status" && h.OldValue == "Open"), Is.True);
        Assert.That(savedTicket.Histories.Any(h => h.FieldName == "Priority" && h.NewValue == "High"), Is.True);
    }

    [Test]
    public async Task Notifications_SaveAndQueryCorrectly()
    {
        // Arrange
        var userId = "test-user";
        var notifications = new[]
        {
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Notification 1",
                Message = "First notification",
                Type = NotificationType.TicketAssigned,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            },
            new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Notification 2",
                Message = "Second notification",
                Type = NotificationType.CommentAdded,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                IsRead = true,
                ReadAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        // Act
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        // Assert
        var userNotifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync();

        Assert.That(userNotifications.Count, Is.EqualTo(2));
        
        var unreadNotifications = userNotifications.Where(n => !n.IsRead).ToList();
        Assert.That(unreadNotifications.Count, Is.EqualTo(1));
        Assert.That(unreadNotifications.First().Title, Is.EqualTo("Notification 1"));

        var readNotifications = userNotifications.Where(n => n.IsRead).ToList();
        Assert.That(readNotifications.Count, Is.EqualTo(1));
        Assert.That(readNotifications.First().ReadAt, Is.Not.Null);
    }

    [Test]
    public async Task ConcurrentTicketUpdates_HandledCorrectly()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Concurrent Update Test",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act - Simulate concurrent updates using separate contexts
        using var context1 = new TicketDbContext(_options);
        using var context2 = new TicketDbContext(_options);

        var ticket1 = await context1.Tickets.FindAsync(ticket.Id);
        var ticket2 = await context2.Tickets.FindAsync(ticket.Id);

        ticket1!.Title = "Updated by User 1";
        ticket2!.Description = "Updated by User 2";

        await context1.SaveChangesAsync();
        await context2.SaveChangesAsync();

        // Assert
        using var verifyContext = new TicketDbContext(_options);
        var finalTicket = await verifyContext.Tickets.FindAsync(ticket.Id);
        
        Assert.That(finalTicket, Is.Not.Null);
        // The last update should win (context2's description update)
        Assert.That(finalTicket.Description, Is.EqualTo("Updated by User 2"));
    }

    [Test]
    public async Task DatabaseSchema_SupportsRequiredEntities()
    {
        // This test verifies that the database schema supports all our core entities

        // Act & Assert - No exceptions should be thrown when creating these entities
        var projectId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var entities = new object[]
        {
            new Project { Id = projectId, Name = "Schema Test", CreatedBy = "user", CreatedAt = DateTime.UtcNow },
            new Ticket { Id = ticketId, ProjectId = projectId, Title = "Schema Test", CreatedBy = "user", CreatedAt = DateTime.UtcNow },
            new Comment { Id = Guid.NewGuid(), TicketId = ticketId, Content = "Test", AuthorId = "user", CreatedAt = DateTime.UtcNow },
            new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = "user", Role = ProjectRole.Member, JoinedAt = DateTime.UtcNow },
            new TicketAssignment { Id = Guid.NewGuid(), TicketId = ticketId, AssigneeId = "user", AssignedBy = "assigner", AssignedAt = DateTime.UtcNow },
            new TicketHistory { Id = Guid.NewGuid(), TicketId = ticketId, FieldName = "Test", ChangedBy = "user", ChangedAt = DateTime.UtcNow, ActionType = HistoryActionType.Created },
            new Notification { Id = Guid.NewGuid(), UserId = "user", Title = "Test", Message = "Test", Type = NotificationType.TicketAssigned, CreatedAt = DateTime.UtcNow }
        };

        foreach (var entity in entities)
        {
            _context.Add(entity);
        }

        // Should not throw any exceptions
        Assert.DoesNotThrowAsync(async () => await _context.SaveChangesAsync());
        
        // Verify all entities were saved
        Assert.That(await _context.Projects.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.Tickets.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.Comments.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.ProjectMembers.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.TicketAssignments.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.TicketHistories.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.Notifications.CountAsync(), Is.EqualTo(1));
    }
}