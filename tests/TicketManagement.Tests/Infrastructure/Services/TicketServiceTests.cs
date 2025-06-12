using Moq;
using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Infrastructure.Services;

namespace TicketManagement.Tests.Infrastructure.Services;

[TestFixture]
public class TicketServiceTests
{
    private Mock<ITicketRepository> _mockTicketRepository;
    private Mock<IProjectRepository> _mockProjectRepository;
    private Mock<ITicketAssignmentRepository> _mockAssignmentRepository;
    private Mock<ICommentRepository> _mockCommentRepository;
    private Mock<ITicketHistoryRepository> _mockHistoryRepository;
    private Mock<INotificationService> _mockNotificationService;
    private TicketService _service;

    [SetUp]
    public void Setup()
    {
        _mockTicketRepository = new Mock<ITicketRepository>();
        _mockProjectRepository = new Mock<IProjectRepository>();
        _mockAssignmentRepository = new Mock<ITicketAssignmentRepository>();
        _mockCommentRepository = new Mock<ICommentRepository>();
        _mockHistoryRepository = new Mock<ITicketHistoryRepository>();
        _mockNotificationService = new Mock<INotificationService>();

        _service = new TicketService(
            _mockTicketRepository.Object,
            _mockProjectRepository.Object,
            _mockAssignmentRepository.Object,
            _mockCommentRepository.Object,
            _mockHistoryRepository.Object,
            _mockNotificationService.Object);
    }

    [Test]
    public async Task CreateTicketAsync_ValidData_CreatesTicket()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var title = "Test Ticket";
        var description = "Test Description";
        var createdBy = "test-user";
        var priority = TicketPriority.High;

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var expectedTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Description = description,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        _mockTicketRepository.Setup(r => r.AddAsync(It.IsAny<Ticket>()))
            .ReturnsAsync(expectedTicket);

        // Act
        var result = await _service.CreateTicketAsync(
            projectId, title, description, createdBy, priority);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo(title));
        Assert.That(result.Description, Is.EqualTo(description));
        Assert.That(result.Priority, Is.EqualTo(priority));
        Assert.That(result.Status, Is.EqualTo(TicketStatus.Open));
        Assert.That(result.CreatedBy, Is.EqualTo(createdBy));

        _mockTicketRepository.Verify(r => r.AddAsync(It.Is<Ticket>(t => 
            t.ProjectId == projectId && 
            t.Title == title && 
            t.Description == description)), Times.Once);
    }

    [Test]
    public async Task CreateTicketAsync_ProjectNotFound_ThrowsArgumentException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        
        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync((Project)null!);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateTicketAsync(projectId, "Title", "Description", "user"));
    }

    [Test]
    public async Task CreateTicketAsync_EmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Test Project", CreatedBy = "creator", CreatedAt = DateTime.UtcNow };
        
        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateTicketAsync(projectId, "", "Description", "user"));
    }

    [Test]
    public async Task UpdateTicketAsync_ExistingTicket_UpdatesTicket()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var existingTicket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Old Title",
            Description = "Old Description",
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open,
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var newTitle = "Updated Title";
        var newDescription = "Updated Description";
        var newPriority = TicketPriority.High;
        var updatedBy = "updater";

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(existingTicket);

        _mockTicketRepository.Setup(r => r.UpdateAsync(It.IsAny<Ticket>()))
            .ReturnsAsync((Ticket t) => t);

        // Act
        var result = await _service.UpdateTicketAsync(
            ticketId, newTitle, newDescription, newPriority, "", Array.Empty<string>(), null, updatedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Title, Is.EqualTo(newTitle));
        Assert.That(result.Description, Is.EqualTo(newDescription));
        Assert.That(result.Priority, Is.EqualTo(newPriority));
        Assert.That(result.UpdatedBy, Is.EqualTo(updatedBy));

        _mockTicketRepository.Verify(r => r.UpdateAsync(It.Is<Ticket>(t => 
            t.Title == newTitle && 
            t.Description == newDescription && 
            t.Priority == newPriority)), Times.Once);
    }

    [Test]
    public async Task UpdateTicketAsync_NonExistingTicket_ThrowsArgumentException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        
        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync((Ticket)null!);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.UpdateTicketAsync(ticketId, "Title", "Description", TicketPriority.Medium, "", Array.Empty<string>(), null, "user"));
    }

    [Test]
    public async Task UpdateTicketStatusAsync_ValidTransition_UpdatesStatus()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            Status = TicketStatus.Open,
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var newStatus = TicketStatus.InProgress;
        var updatedBy = "updater";

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockTicketRepository.Setup(r => r.UpdateAsync(It.IsAny<Ticket>()))
            .ReturnsAsync((Ticket t) => t);

        // Act
        var result = await _service.UpdateTicketStatusAsync(ticketId, newStatus, updatedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(newStatus));
        Assert.That(result.UpdatedBy, Is.EqualTo(updatedBy));

        _mockTicketRepository.Verify(r => r.UpdateAsync(It.Is<Ticket>(t => 
            t.Status == newStatus)), Times.Once);
    }

    [Test]
    public async Task UpdateTicketStatusAsync_InvalidTransition_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            Status = TicketStatus.InProgress, // Cannot transition back to Open based on our logic
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var invalidStatus = TicketStatus.Open;
        var updatedBy = "updater";

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.UpdateTicketStatusAsync(ticketId, invalidStatus, updatedBy));
    }

    [Test]
    public async Task AssignTicketAsync_ValidData_AssignsTicket()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var assigneeId = "assignee";
        var assignedBy = "assigner";

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var expectedAssignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssigneeId = assigneeId,
            AssignedBy = assignedBy,
            AssignedAt = DateTime.UtcNow
        };

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockAssignmentRepository.Setup(r => r.GetActiveAssignmentAsync(ticketId, assigneeId))
            .ReturnsAsync((TicketAssignment)null!);

        _mockAssignmentRepository.Setup(r => r.AddAsync(It.IsAny<TicketAssignment>()))
            .ReturnsAsync(expectedAssignment);

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        // Act
        var result = await _service.AssignTicketAsync(ticketId, assigneeId, assignedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(ticketId));

        _mockAssignmentRepository.Verify(r => r.AddAsync(It.Is<TicketAssignment>(a => 
            a.TicketId == ticketId && 
            a.AssigneeId == assigneeId && 
            a.AssignedBy == assignedBy)), Times.Once);
    }

    [Test]
    public async Task AssignTicketAsync_AlreadyAssigned_ThrowsInvalidOperationException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var assigneeId = "assignee";
        var assignedBy = "assigner";

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var existingAssignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssigneeId = assigneeId,
            AssignedBy = "previous-assigner",
            AssignedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockAssignmentRepository.Setup(r => r.GetActiveAssignmentAsync(ticketId, assigneeId))
            .ReturnsAsync(existingAssignment);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.AssignTicketAsync(ticketId, assigneeId, assignedBy));
    }

    [Test]
    public async Task AddCommentAsync_ValidData_AddsComment()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var content = "Test comment content";
        var authorId = "commenter";

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var expectedComment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Content = content,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow
        };

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockCommentRepository.Setup(r => r.AddAsync(It.IsAny<Comment>()))
            .ReturnsAsync(expectedComment);

        // Act
        var result = await _service.AddCommentAsync(ticketId, content, authorId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Content, Is.EqualTo(content));
        Assert.That(result.AuthorId, Is.EqualTo(authorId));
        Assert.That(result.TicketId, Is.EqualTo(ticketId));

        _mockCommentRepository.Verify(r => r.AddAsync(It.Is<Comment>(c => 
            c.TicketId == ticketId && 
            c.Content == content && 
            c.AuthorId == authorId)), Times.Once);
    }

    [Test]
    public async Task AddCommentAsync_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var ticket = new Ticket { Id = ticketId, ProjectId = Guid.NewGuid(), Title = "Test", CreatedBy = "creator", CreatedAt = DateTime.UtcNow };
        
        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.AddCommentAsync(ticketId, "", "author"));
    }

    [Test]
    public async Task GetTicketsByProjectAsync_ReturnsProjectTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var expectedTickets = new[]
        {
            new Ticket { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Ticket 1", CreatedBy = "user1", CreatedAt = DateTime.UtcNow },
            new Ticket { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Ticket 2", CreatedBy = "user2", CreatedAt = DateTime.UtcNow }
        };

        _mockTicketRepository.Setup(r => r.GetTicketsByProjectIdAsync(projectId))
            .ReturnsAsync(expectedTickets);

        // Act
        var result = await _service.GetTicketsByProjectAsync(projectId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        _mockTicketRepository.Verify(r => r.GetTicketsByProjectIdAsync(projectId), Times.Once);
    }

    [Test]
    public async Task CanUserAccessTicketAsync_UserHasAccess_ReturnsTrue()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = "test-user";
        var projectId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Test Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CanUserAccessTicketAsync(ticketId, userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CanUserAccessTicketAsync_UserNoAccess_ReturnsFalse()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var userId = "test-user";
        var projectId = Guid.NewGuid();

        var ticket = new Ticket
        {
            Id = ticketId,
            ProjectId = projectId,
            Title = "Test Ticket",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _mockTicketRepository.Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CanUserAccessTicketAsync(ticketId, userId);

        // Assert
        Assert.That(result, Is.False);
    }
}