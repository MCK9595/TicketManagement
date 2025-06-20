using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;
using TicketManagement.ApiService.Controllers;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Api.Controllers;

[TestFixture]
public class TicketsControllerAssignTests
{
    private Mock<ITicketService> _mockTicketService;
    private Mock<IProjectService> _mockProjectService;
    private Mock<IUserManagementService> _mockUserManagementService;
    private Mock<ILogger<TicketsController>> _mockLogger;
    private TicketsController _controller;
    private string _userId;
    private Guid _ticketId;

    [SetUp]
    public void Setup()
    {
        _mockTicketService = new Mock<ITicketService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockUserManagementService = new Mock<IUserManagementService>();
        _mockLogger = new Mock<ILogger<TicketsController>>();
        _controller = new TicketsController(
            _mockTicketService.Object, 
            _mockProjectService.Object,
            _mockUserManagementService.Object,
            _mockLogger.Object);
        
        _userId = "test-user-id";
        _ticketId = Guid.NewGuid();

        // Setup user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId),
            new Claim("sub", _userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        // Setup UserManagementService mock
        var users = new Dictionary<string, UserDto>
        {
            { _userId, new UserDto { Id = _userId, Username = "testuser", Email = "test@example.com", DisplayName = "Test User" } },
            { "assignee-user-id", new UserDto { Id = "assignee-user-id", Username = "assigneeuser", Email = "assignee@example.com", DisplayName = "Assignee User" } },
            { "new-assignee-id", new UserDto { Id = "new-assignee-id", Username = "newassignee", Email = "new@example.com", DisplayName = "New Assignee" } },
            { "existing-assignee", new UserDto { Id = "existing-assignee", Username = "existingassignee", Email = "existing@example.com", DisplayName = "Existing Assignee" } },
            { "old-assignee", new UserDto { Id = "old-assignee", Username = "oldassignee", Email = "old@example.com", DisplayName = "Old Assignee" } }
        };
        _mockUserManagementService.Setup(x => x.GetUsersByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(users);
    }

    [Test]
    public async Task AssignTicketSimple_AssignToNewUser_Success()
    {
        // Arrange
        var assigneeId = "assignee-user-id";
        var dto = new AssignTicketDto { AssigneeId = assigneeId };
        
        var ticket = new Ticket
        {
            Id = _ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            Assignments = new List<TicketAssignment>()
        };

        var updatedTicket = new Ticket
        {
            Id = _ticketId,
            ProjectId = ticket.ProjectId,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedBy = ticket.CreatedBy,
            CreatedAt = ticket.CreatedAt,
            Assignments = new List<TicketAssignment>
            {
                new TicketAssignment
                {
                    Id = Guid.NewGuid(),
                    TicketId = _ticketId,
                    AssigneeId = assigneeId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = _userId
                }
            }
        };

        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.GetTicketAsync(_ticketId))
            .ReturnsAsync(ticket);
        _mockTicketService.Setup(s => s.AssignTicketAsync(_ticketId, assigneeId, _userId))
            .ReturnsAsync(updatedTicket);

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<TicketAssignmentDto>>());
        var response = result.Value as ApiResponseDto<TicketAssignmentDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.AssigneeId, Is.EqualTo(assigneeId));
        Assert.That(response.Data.AssigneeName, Is.EqualTo("Assignee User"));
        
        // Verify service calls
        _mockTicketService.Verify(s => s.GetTicketAsync(_ticketId), Times.Once);
        _mockTicketService.Verify(s => s.AssignTicketAsync(_ticketId, assigneeId, _userId), Times.Once);
    }

    [Test]
    public async Task AssignTicketSimple_RemoveAllAssignments_Success()
    {
        // Arrange
        var dto = new AssignTicketDto { AssigneeId = "" }; // Empty assignee ID to remove assignments
        
        var existingAssignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticketId,
            AssigneeId = "existing-assignee",
            AssignedAt = DateTime.UtcNow,
            AssignedBy = _userId
        };

        var ticket = new Ticket
        {
            Id = _ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            Assignments = new List<TicketAssignment> { existingAssignment }
        };

        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.GetTicketAsync(_ticketId))
            .ReturnsAsync(ticket);

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<TicketAssignmentDto>>());
        var response = result.Value as ApiResponseDto<TicketAssignmentDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        
        // Verify service calls
        _mockTicketService.Verify(s => s.GetTicketAsync(_ticketId), Times.Once);
        _mockTicketService.Verify(s => s.RemoveTicketAssignmentAsync(_ticketId, existingAssignment.AssigneeId, _userId), Times.Once);
        _mockTicketService.Verify(s => s.AssignTicketAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AssignTicketSimple_ReplaceExistingAssignment_Success()
    {
        // Arrange
        var newAssigneeId = "new-assignee-id";
        var dto = new AssignTicketDto { AssigneeId = newAssigneeId };
        
        var existingAssignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = _ticketId,
            AssigneeId = "old-assignee",
            AssignedAt = DateTime.UtcNow.AddHours(-1),
            AssignedBy = _userId
        };

        var ticket = new Ticket
        {
            Id = _ticketId,
            ProjectId = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            Assignments = new List<TicketAssignment> { existingAssignment }
        };

        var updatedTicket = new Ticket
        {
            Id = _ticketId,
            ProjectId = ticket.ProjectId,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedBy = ticket.CreatedBy,
            CreatedAt = ticket.CreatedAt,
            Assignments = new List<TicketAssignment>
            {
                new TicketAssignment
                {
                    Id = Guid.NewGuid(),
                    TicketId = _ticketId,
                    AssigneeId = newAssigneeId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = _userId
                }
            }
        };

        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.GetTicketAsync(_ticketId))
            .ReturnsAsync(ticket);
        _mockTicketService.Setup(s => s.AssignTicketAsync(_ticketId, newAssigneeId, _userId))
            .ReturnsAsync(updatedTicket);

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<TicketAssignmentDto>>());
        var response = result.Value as ApiResponseDto<TicketAssignmentDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.AssigneeId, Is.EqualTo(newAssigneeId));
        Assert.That(response.Data.AssigneeName, Is.EqualTo("New Assignee"));
        
        // Verify service calls
        _mockTicketService.Verify(s => s.GetTicketAsync(_ticketId), Times.Once); // Called once to check existing assignments before new assignment
        _mockTicketService.Verify(s => s.RemoveTicketAssignmentAsync(_ticketId, existingAssignment.AssigneeId, _userId), Times.Once);
        _mockTicketService.Verify(s => s.AssignTicketAsync(_ticketId, newAssigneeId, _userId), Times.Once);
    }

    [Test]
    public async Task AssignTicketSimple_UserCannotAccessTicket_ReturnsForbid()
    {
        // Arrange
        var dto = new AssignTicketDto { AssigneeId = "assignee-id" };
        
        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task AssignTicketSimple_TicketServiceThrowsInvalidOperationException_ReturnsBadRequest()
    {
        // Arrange
        var dto = new AssignTicketDto { AssigneeId = "assignee-id" };
        
        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.GetTicketAsync(_ticketId))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequestResult = result.Result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        
        var response = badRequestResult.Value as ApiResponseDto<TicketAssignmentDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.False);
        Assert.That(response.Errors, Does.Contain("Database connection failed"));
    }

    [Test]
    public async Task AssignTicketSimple_TicketServiceThrowsUnexpectedException_ReturnsInternalServerError()
    {
        // Arrange
        var dto = new AssignTicketDto { AssigneeId = "assignee-id" };
        
        _mockTicketService.Setup(s => s.CanUserAccessTicketAsync(_ticketId, _userId))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.GetTicketAsync(_ticketId))
            .ThrowsAsync(new Exception("Unexpected database error"));

        // Act
        var result = await _controller.AssignTicketSimple(_ticketId, dto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult, Is.Not.Null);
        Assert.That(objectResult.StatusCode, Is.EqualTo(500));
        
        var response = objectResult.Value as ApiResponseDto<TicketAssignmentDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.False);
        Assert.That(response.Errors, Does.Contain("Internal server error: Unexpected database error"));
    }
}