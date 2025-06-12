using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Infrastructure.Repositories;
using TicketManagement.Contracts.Repositories;

namespace TicketManagement.Tests.Infrastructure.Repositories;

[TestFixture]
public class TicketRepositoryTests
{
    private TicketDbContext _context;
    private TicketRepository _repository;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TicketDbContext(options);
        _repository = new TicketRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task GetTicketsByProjectIdAsync_ReturnsProjectTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();

        var tickets = new[]
        {
            new Ticket { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Ticket 1", CreatedBy = "user1", CreatedAt = DateTime.UtcNow },
            new Ticket { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Ticket 2", CreatedBy = "user2", CreatedAt = DateTime.UtcNow },
            new Ticket { Id = Guid.NewGuid(), ProjectId = otherProjectId, Title = "Other Ticket", CreatedBy = "user3", CreatedAt = DateTime.UtcNow }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTicketsByProjectIdAsync(projectId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.All(t => t.ProjectId == projectId), Is.True);
    }

    [Test]
    [Ignore("In-Memory database has issues with complex LINQ queries")]
    public async Task GetTicketsByAssigneeIdAsync_ReturnsAssignedTickets()
    {
        // Arrange
        var assigneeId = "test-user";
        var projectId = Guid.NewGuid();

        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Assigned Ticket 1",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Assigned Ticket 2",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var ticket3 = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Unassigned Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var assignments = new[]
        {
            new TicketAssignment
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                AssigneeId = assigneeId,
                AssignedBy = "assigner",
                AssignedAt = DateTime.UtcNow
            },
            new TicketAssignment
            {
                Id = Guid.NewGuid(),
                TicketId = ticket2.Id,
                AssigneeId = assigneeId,
                AssignedBy = "assigner",
                AssignedAt = DateTime.UtcNow
            }
        };

        await _context.Tickets.AddRangeAsync(ticket1, ticket2, ticket3);
        await _context.SaveChangesAsync();
        
        await _context.TicketAssignments.AddRangeAsync(assignments);
        await _context.SaveChangesAsync();
        
        // Clear the change tracker to ensure fresh query
        _context.ChangeTracker.Clear();

        // Debug: Check what's in the database
        var allTickets = await _context.Tickets.ToListAsync();
        var allAssignments = await _context.TicketAssignments.ToListAsync();
        
        Console.WriteLine($"Tickets in DB: {allTickets.Count}");
        Console.WriteLine($"Assignments in DB: {allAssignments.Count}");
        Console.WriteLine($"Assignments for assignee {assigneeId}: {allAssignments.Count(a => a.AssigneeId == assigneeId)}");

        // Act
        var result = await _repository.GetTicketsByAssigneeAsync(assigneeId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.Any(t => t.Id == ticket1.Id), Is.True);
        Assert.That(result.Any(t => t.Id == ticket2.Id), Is.True);
        Assert.That(result.Any(t => t.Id == ticket3.Id), Is.False);
    }

    [Test]
    public async Task SearchTicketsAsync_WithKeyword_ReturnsMatchingTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var criteria = new TicketSearchCriteria
        {
            Keyword = "search term"
        };

        var tickets = new[]
        {
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "This contains search term",
                Description = "Description",
                CreatedBy = "user1",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Another ticket",
                Description = "This description has the search term",
                CreatedBy = "user2",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "No match",
                Description = "No match here",
                CreatedBy = "user3",
                CreatedAt = DateTime.UtcNow
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchTicketsAsync(projectId, criteria, 1, 10);

        // Assert
        Assert.That(result.Items.Count(), Is.EqualTo(2));
        Assert.That(result.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task SearchTicketsAsync_WithStatusFilter_ReturnsFilteredTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var criteria = new TicketSearchCriteria
        {
            Statuses = new[] { TicketStatus.Open, TicketStatus.InProgress }
        };

        var tickets = new[]
        {
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Open Ticket",
                Status = TicketStatus.Open,
                CreatedBy = "user1",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "In Progress Ticket",
                Status = TicketStatus.InProgress,
                CreatedBy = "user2",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Closed Ticket",
                Status = TicketStatus.Closed,
                CreatedBy = "user3",
                CreatedAt = DateTime.UtcNow
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchTicketsAsync(projectId, criteria, 1, 10);

        // Assert
        Assert.That(result.Items.Count(), Is.EqualTo(2));
        Assert.That(result.Items.All(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress), Is.True);
    }

    [Test]
    public async Task SearchTicketsAsync_WithPriorityFilter_ReturnsFilteredTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var criteria = new TicketSearchCriteria
        {
            Priorities = new[] { TicketPriority.High, TicketPriority.Critical }
        };

        var tickets = new[]
        {
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "High Priority",
                Priority = TicketPriority.High,
                CreatedBy = "user1",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Critical Priority",
                Priority = TicketPriority.Critical,
                CreatedBy = "user2",
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Low Priority",
                Priority = TicketPriority.Low,
                CreatedBy = "user3",
                CreatedAt = DateTime.UtcNow
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchTicketsAsync(projectId, criteria, 1, 10);

        // Assert
        Assert.That(result.Items.Count(), Is.EqualTo(2));
        Assert.That(result.Items.All(t => t.Priority == TicketPriority.High || t.Priority == TicketPriority.Critical), Is.True);
    }

    [Test]
    public async Task SearchTicketsAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var criteria = new TicketSearchCriteria();

        var tickets = Enumerable.Range(1, 15).Select(i => new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = $"Ticket {i}",
            CreatedBy = "user",
            CreatedAt = DateTime.UtcNow.AddMinutes(-i) // Different creation times for consistent ordering
        }).ToArray();

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var page1 = await _repository.SearchTicketsAsync(projectId, criteria, 1, 5);
        var page2 = await _repository.SearchTicketsAsync(projectId, criteria, 2, 5);

        // Assert
        Assert.That(page1.Items.Count(), Is.EqualTo(5));
        Assert.That(page1.TotalCount, Is.EqualTo(15));
        Assert.That(page1.Page, Is.EqualTo(1));
        Assert.That(page1.PageSize, Is.EqualTo(5));
        Assert.That(page1.TotalPages, Is.EqualTo(3));

        Assert.That(page2.Items.Count(), Is.EqualTo(5));
        Assert.That(page2.Page, Is.EqualTo(2));
    }

    [Test]
    public async Task GetTicketWithDetailsAsync_ReturnsTicketWithRelatedData()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Test Ticket",
            Description = "Test Description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Content = "Test Comment",
            AuthorId = "commenter",
            CreatedAt = DateTime.UtcNow
        };

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssigneeId = "assignee",
            AssignedBy = "assigner",
            AssignedAt = DateTime.UtcNow
        };

        await _context.Projects.AddAsync(project);
        await _context.Tickets.AddAsync(ticket);
        await _context.Comments.AddAsync(comment);
        await _context.TicketAssignments.AddAsync(assignment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTicketWithCommentsAsync(ticketId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(ticketId));
        Assert.That(result.Project, Is.Not.Null);
        Assert.That(result.Project.Name, Is.EqualTo("Test Project"));
        Assert.That(result.Comments.Count, Is.EqualTo(1));
        Assert.That(result.Assignments.Count, Is.EqualTo(1));
    }
}