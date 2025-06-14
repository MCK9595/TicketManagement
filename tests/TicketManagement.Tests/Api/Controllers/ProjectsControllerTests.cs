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
public class ProjectsControllerTests
{
    private Mock<IProjectService> _mockProjectService;
    private Mock<ILogger<ProjectsController>> _mockLogger;
    private ProjectsController _controller;
    private string _userId;

    [SetUp]
    public void Setup()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockLogger = new Mock<ILogger<ProjectsController>>();
        _controller = new ProjectsController(_mockProjectService.Object, _mockLogger.Object);
        _userId = "test-user-id";

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
    }

    [Test]
    public async Task GetProjects_ReturnsUserProjects()
    {
        // Arrange
        var projects = new List<Project>
        {
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Project 1",
                Description = "Description 1",
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Members = new List<ProjectMember>(),
                Tickets = new List<Ticket>()
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Project 2",
                Description = "Description 2",
                CreatedBy = "other-user",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Members = new List<ProjectMember>(),
                Tickets = new List<Ticket>()
            }
        };

        _mockProjectService.Setup(s => s.GetProjectsByUserAsync(_userId))
            .ReturnsAsync(projects);

        // Act
        var result = await _controller.GetProjects();

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<List<ProjectDto>>>());
        var response = result.Value as ApiResponseDto<List<ProjectDto>>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Count, Is.EqualTo(2));
        Assert.That(response.Data[0].Name, Is.EqualTo("Project 1"));
    }

    [Test]
    public async Task GetProject_ExistingProject_ReturnsProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            Description = "Test Description",
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Members = new List<ProjectMember>(),
            Tickets = new List<Ticket>()
        };

        _mockProjectService.Setup(s => s.CanUserAccessProjectAsync(projectId, _userId))
            .ReturnsAsync(true);
        _mockProjectService.Setup(s => s.GetProjectAsync(projectId))
            .ReturnsAsync(project);

        // Act
        var result = await _controller.GetProject(projectId);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<ProjectDto>>());
        var response = result.Value as ApiResponseDto<ProjectDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.Id, Is.EqualTo(projectId));
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task GetProject_UserCannotAccess_ReturnsForbid()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        _mockProjectService.Setup(s => s.CanUserAccessProjectAsync(projectId, _userId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.GetProject(projectId);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task GetProject_ProjectNotFound_ReturnsNotFound()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        _mockProjectService.Setup(s => s.CanUserAccessProjectAsync(projectId, _userId))
            .ReturnsAsync(true);
        _mockProjectService.Setup(s => s.GetProjectAsync(projectId))
            .ReturnsAsync((Project)null!);

        // Act
        var result = await _controller.GetProject(projectId);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
        var notFoundResult = result.Result as NotFoundObjectResult;
        Assert.That(notFoundResult, Is.Not.Null);

        var response = notFoundResult.Value as ApiResponseDto<ProjectDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.False);
    }

    [Test]
    public async Task CreateProject_ValidData_CreatesProject()
    {
        // Arrange
        var createDto = new CreateProjectDto
        {
            Name = "New Project",
            Description = "New Description"
        };

        var createdProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = createDto.Name,
            Description = createDto.Description,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Members = new List<ProjectMember>(),
            Tickets = new List<Ticket>()
        };

        _mockProjectService.Setup(s => s.CreateProjectAsync(It.IsAny<Guid>(), createDto.Name, createDto.Description, _userId))
            .ReturnsAsync(createdProject);

        // Act
        var result = await _controller.CreateProject(createDto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedResult>());
        var createdResult = result.Result as CreatedResult;
        Assert.That(createdResult, Is.Not.Null);

        var response = createdResult.Value as ApiResponseDto<ProjectDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.Name, Is.EqualTo(createDto.Name));
        Assert.That(response.Data.Description, Is.EqualTo(createDto.Description));
    }

    [Test]
    public async Task UpdateProject_ValidData_UpdatesProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var updateDto = new UpdateProjectDto
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        var updatedProject = new Project
        {
            Id = projectId,
            Name = updateDto.Name,
            Description = updateDto.Description,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            Members = new List<ProjectMember>(),
            Tickets = new List<Ticket>()
        };

        _mockProjectService.Setup(s => s.CanUserManageProjectAsync(projectId, _userId))
            .ReturnsAsync(true);
        _mockProjectService.Setup(s => s.UpdateProjectAsync(projectId, updateDto.Name, updateDto.Description, _userId))
            .ReturnsAsync(updatedProject);

        // Act
        var result = await _controller.UpdateProject(projectId, updateDto);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<ProjectDto>>());
        var response = result.Value as ApiResponseDto<ProjectDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.Name, Is.EqualTo(updateDto.Name));
        Assert.That(response.Data.Description, Is.EqualTo(updateDto.Description));
    }

    [Test]
    public async Task UpdateProject_UserCannotManage_ReturnsForbid()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var updateDto = new UpdateProjectDto
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        _mockProjectService.Setup(s => s.CanUserManageProjectAsync(projectId, _userId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateProject(projectId, updateDto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task AddProjectMember_ValidData_AddsMember()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var addMemberDto = new AddProjectMemberDto
        {
            UserId = "new-member",
            Role = ProjectRole.Member
        };

        var addedMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = addMemberDto.UserId,
            Role = addMemberDto.Role,
            JoinedAt = DateTime.UtcNow
        };

        _mockProjectService.Setup(s => s.CanUserManageProjectAsync(projectId, _userId))
            .ReturnsAsync(true);
        _mockProjectService.Setup(s => s.AddMemberAsync(projectId, addMemberDto.UserId, addMemberDto.Role, _userId))
            .ReturnsAsync(addedMember);

        // Act
        var result = await _controller.AddProjectMember(projectId, addMemberDto);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<ProjectMemberDto>>());
        var response = result.Value as ApiResponseDto<ProjectMemberDto>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.UserId, Is.EqualTo(addMemberDto.UserId));
        Assert.That(response.Data.Role, Is.EqualTo(addMemberDto.Role));
    }

    [Test]
    public async Task AddProjectMember_UserCannotManage_ReturnsForbid()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var addMemberDto = new AddProjectMemberDto
        {
            UserId = "new-member",
            Role = ProjectRole.Member
        };

        _mockProjectService.Setup(s => s.CanUserManageProjectAsync(projectId, _userId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.AddProjectMember(projectId, addMemberDto);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task GetProjectMembers_ValidProject_ReturnsMembers()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var members = new List<ProjectMember>
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

        _mockProjectService.Setup(s => s.CanUserAccessProjectAsync(projectId, _userId))
            .ReturnsAsync(true);
        _mockProjectService.Setup(s => s.GetProjectMembersAsync(projectId))
            .ReturnsAsync(members);

        // Act
        var result = await _controller.GetProjectMembers(projectId);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<List<ProjectMemberDto>>>());
        var response = result.Value as ApiResponseDto<List<ProjectMemberDto>>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task RemoveProjectMember_ValidData_RemovesMember()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userIdToRemove = "member-to-remove";

        _mockProjectService.Setup(s => s.CanUserManageProjectAsync(projectId, _userId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveProjectMember(projectId, userIdToRemove);

        // Assert
        Assert.That(result.Value, Is.TypeOf<ApiResponseDto<string>>());
        var response = result.Value as ApiResponseDto<string>;
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);

        _mockProjectService.Verify(s => s.RemoveMemberAsync(projectId, userIdToRemove, _userId), Times.Once);
    }
}