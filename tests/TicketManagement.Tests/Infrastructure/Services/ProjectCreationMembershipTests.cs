using Moq;
using NUnit.Framework;
using Microsoft.Extensions.Logging;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Infrastructure.Services;

namespace TicketManagement.Tests.Infrastructure.Services;

[TestFixture]
public class ProjectCreationMembershipTests
{
    private Mock<IProjectRepository> _mockProjectRepository;
    private Mock<IOrganizationService> _mockOrganizationService;
    private Mock<INotificationService> _mockNotificationService;
    private Mock<ICacheService> _mockCacheService;
    private Mock<ILogger<ProjectService>> _mockLogger;
    private ProjectService _service;

    [SetUp]
    public void Setup()
    {
        _mockProjectRepository = new Mock<IProjectRepository>();
        _mockOrganizationService = new Mock<IOrganizationService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<ProjectService>>();

        // Setup cache to properly handle cache miss scenario
        _mockCacheService.Setup(c => c.GetAsync<IEnumerable<Project>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Project>)null!);
        _mockCacheService.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Project>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockCacheService.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new ProjectService(
            _mockProjectRepository.Object, 
            _mockOrganizationService.Object,
            _mockNotificationService.Object, 
            _mockCacheService.Object, 
            _mockLogger.Object);
    }

    [Test]
    public async Task CreateProjectAsync_CreatorIsAutomaticallyAddedAsAdmin()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var projectName = "Test Project";
        var description = "Test Description";
        var createdBy = "test-user";

        var capturedProject = (Project)null!;
        
        _mockOrganizationService.Setup(s => s.CanUserCreateProjectAsync(organizationId, createdBy))
            .ReturnsAsync(true);
        _mockOrganizationService.Setup(s => s.CanCreateProjectAsync(organizationId))
            .ReturnsAsync(true);

        _mockProjectRepository.Setup(r => r.AddAsync(It.IsAny<Project>()))
            .Callback<Project>(p => capturedProject = p)
            .ReturnsAsync((Project p) => p);

        // Act
        var result = await _service.CreateProjectAsync(organizationId, projectName, description, createdBy);

        // Assert - Basic project properties
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(projectName));
        Assert.That(result.Description, Is.EqualTo(description));
        Assert.That(result.CreatedBy, Is.EqualTo(createdBy));
        Assert.That(result.IsActive, Is.True);
        Assert.That(result.OrganizationId, Is.EqualTo(organizationId));

        // Assert - Creator is automatically added as admin member
        Assert.That(result.Members, Is.Not.Null);
        Assert.That(result.Members.Count, Is.EqualTo(1), "Project should have exactly one member (the creator)");
        
        var creatorMember = result.Members.First();
        Assert.That(creatorMember.UserId, Is.EqualTo(createdBy), "Creator should be added as a member");
        Assert.That(creatorMember.Role, Is.EqualTo(ProjectRole.Admin), "Creator should have Admin role");
        Assert.That(creatorMember.ProjectId, Is.EqualTo(result.Id), "Member should be linked to the correct project");
        Assert.That(creatorMember.JoinedAt, Is.LessThanOrEqualTo(DateTime.UtcNow), "JoinedAt should be set to current time");
        Assert.That(creatorMember.JoinedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)), "JoinedAt should be recent");

        // Verify the project was captured correctly during AddAsync
        Assert.That(capturedProject, Is.Not.Null);
        Assert.That(capturedProject.Members.Count, Is.EqualTo(1));
        Assert.That(capturedProject.Members.First().UserId, Is.EqualTo(createdBy));
        Assert.That(capturedProject.Members.First().Role, Is.EqualTo(ProjectRole.Admin));

        // Verify repository was called with the correct project
        _mockProjectRepository.Verify(r => r.AddAsync(It.Is<Project>(p => 
            p.Members.Count == 1 && 
            p.Members.First().UserId == createdBy && 
            p.Members.First().Role == ProjectRole.Admin)), 
            Times.Once);
    }

    [Test]
    public async Task CreateProjectAsync_AndGetProjectsByUser_ReturnsCreatedProject()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var projectName = "Test Project";
        var description = "Test Description";
        var createdBy = "test-user";

        var createdProject = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = projectName,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Members = new List<ProjectMember>
            {
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    UserId = createdBy,
                    Role = ProjectRole.Admin,
                    JoinedAt = DateTime.UtcNow
                }
            }
        };

        _mockOrganizationService.Setup(s => s.CanUserCreateProjectAsync(organizationId, createdBy))
            .ReturnsAsync(true);
        _mockOrganizationService.Setup(s => s.CanCreateProjectAsync(organizationId))
            .ReturnsAsync(true);

        _mockProjectRepository.Setup(r => r.AddAsync(It.IsAny<Project>()))
            .ReturnsAsync(createdProject);

        // Setup GetProjectsByUserIdAsync to return the created project
        _mockProjectRepository.Setup(r => r.GetProjectsByUserIdAsync(createdBy))
            .ReturnsAsync(new List<Project> { createdProject });

        // Act
        var projectResult = await _service.CreateProjectAsync(organizationId, projectName, description, createdBy);
        var userProjects = await _service.GetProjectsByUserAsync(createdBy);

        // Assert
        Assert.That(projectResult, Is.Not.Null);
        Assert.That(userProjects, Is.Not.Null);
        Assert.That(userProjects.Count(), Is.EqualTo(1), "User should see the project they created");
        
        var userProject = userProjects.First();
        Assert.That(userProject.Id, Is.EqualTo(projectResult.Id));
        Assert.That(userProject.Name, Is.EqualTo(projectName));
        Assert.That(userProject.CreatedBy, Is.EqualTo(createdBy));

        // Verify the repository method was called correctly
        _mockProjectRepository.Verify(r => r.GetProjectsByUserIdAsync(createdBy), Times.Once);
    }

    [Test]
    public async Task CreateProjectAsync_CacheIsInvalidatedForCreator()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var projectName = "Test Project";
        var description = "Test Description";
        var createdBy = "test-user";

        var createdProject = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = projectName,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Members = new List<ProjectMember>
            {
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    UserId = createdBy,
                    Role = ProjectRole.Admin,
                    JoinedAt = DateTime.UtcNow
                }
            }
        };

        _mockOrganizationService.Setup(s => s.CanUserCreateProjectAsync(organizationId, createdBy))
            .ReturnsAsync(true);
        _mockOrganizationService.Setup(s => s.CanCreateProjectAsync(organizationId))
            .ReturnsAsync(true);

        _mockProjectRepository.Setup(r => r.AddAsync(It.IsAny<Project>()))
            .ReturnsAsync(createdProject);

        // Act
        await _service.CreateProjectAsync(organizationId, projectName, description, createdBy);

        // Assert - Verify cache was invalidated for the user
        var expectedCacheKey = $"user:{createdBy}:projects";
        _mockCacheService.Verify(c => c.RemoveAsync(expectedCacheKey, It.IsAny<CancellationToken>()), 
            Times.Once, "Cache should be invalidated for the user after project creation");
    }

    [Test]
    public async Task CanUserAccessProjectAsync_CreatorCanAccessCreatedProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var createdBy = "test-user";

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, createdBy))
            .ReturnsAsync(true);

        // Act
        var canAccess = await _service.CanUserAccessProjectAsync(projectId, createdBy);

        // Assert
        Assert.That(canAccess, Is.True, "Project creator should be able to access their created project");
        _mockProjectRepository.Verify(r => r.IsUserMemberOfProjectAsync(projectId, createdBy), Times.Once);
    }
}