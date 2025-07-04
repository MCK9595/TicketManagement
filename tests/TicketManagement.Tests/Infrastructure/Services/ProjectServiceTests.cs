using Moq;
using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Infrastructure.Services;

namespace TicketManagement.Tests.Infrastructure.Services;

[TestFixture]
public class ProjectServiceTests
{
    private Mock<IProjectRepository> _mockProjectRepository;
    private Mock<IOrganizationService> _mockOrganizationService;
    private Mock<INotificationService> _mockNotificationService;
    private Mock<ICacheService> _mockCacheService;
    private Mock<Microsoft.Extensions.Logging.ILogger<ProjectService>> _mockLogger;
    private ProjectService _service;

    [SetUp]
    public void Setup()
    {
        _mockProjectRepository = new Mock<IProjectRepository>();
        _mockOrganizationService = new Mock<IOrganizationService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ProjectService>>();
        _service = new ProjectService(
            _mockProjectRepository.Object, 
            _mockOrganizationService.Object,
            _mockNotificationService.Object, 
            _mockCacheService.Object, 
            _mockLogger.Object);
    }

    [Test]
    public async Task GetProjectAsync_ExistingProject_ReturnsProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var expectedProject = new Project
        {
            Id = projectId,
            Name = "Test Project",
            Description = "Test Description",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _mockProjectRepository.Setup(r => r.GetProjectWithMembersAsync(projectId))
            .ReturnsAsync(expectedProject);

        _mockCacheService.Setup(c => c.GetAsync<Project>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Project);

        // Act
        var result = await _service.GetProjectAsync(projectId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(projectId));
        Assert.That(result.Name, Is.EqualTo("Test Project"));
    }

    [Test]
    public async Task GetProjectAsync_NonExistingProject_ReturnsNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _mockProjectRepository.Setup(r => r.GetProjectWithMembersAsync(projectId))
            .ReturnsAsync((Project)null!);

        _mockCacheService.Setup(c => c.GetAsync<Project>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as Project);

        // Act
        var result = await _service.GetProjectAsync(projectId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CreateProjectAsync_ValidData_CreatesProject()
    {
        // Arrange
        var name = "New Project";
        var description = "New Description";
        var createdBy = "test-user";

        var expectedProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _mockProjectRepository.Setup(r => r.AddAsync(It.IsAny<Project>()))
            .ReturnsAsync(expectedProject);

        // Act
        var organizationId = Guid.NewGuid();
        
        // Setup organization service mocks
        _mockOrganizationService.Setup(s => s.CanUserCreateProjectAsync(organizationId, createdBy))
            .ReturnsAsync(true);
        _mockOrganizationService.Setup(s => s.CanCreateProjectAsync(organizationId))
            .ReturnsAsync(true);
        
        var result = await _service.CreateProjectAsync(organizationId, name, description, createdBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(name));
        Assert.That(result.Description, Is.EqualTo(description));
        Assert.That(result.CreatedBy, Is.EqualTo(createdBy));
        Assert.That(result.IsActive, Is.True);

        _mockProjectRepository.Verify(r => r.AddAsync(It.IsAny<Project>()), Times.Once);
    }

    [Test]
    public async Task CreateProjectAsync_EmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateProjectAsync(Guid.NewGuid(), "", "description", "user"));
    }

    [Test]
    public async Task CreateProjectAsync_EmptyCreatedBy_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateProjectAsync(Guid.NewGuid(), "Project Name", "description", ""));
    }

    [Test]
    public async Task UpdateProjectAsync_ExistingProject_UpdatesProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var existingProject = new Project
        {
            Id = projectId,
            Name = "Old Name",
            Description = "Old Description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var newName = "Updated Name";
        var newDescription = "Updated Description";
        var updatedBy = "updater";

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(existingProject);

        _mockProjectRepository.Setup(r => r.UpdateAsync(It.IsAny<Project>()))
            .ReturnsAsync((Project p) => p);

        // Act
        var result = await _service.UpdateProjectAsync(projectId, newName, newDescription, updatedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(newName));
        Assert.That(result.Description, Is.EqualTo(newDescription));

        _mockProjectRepository.Verify(r => r.UpdateAsync(It.Is<Project>(p => 
            p.Name == newName && p.Description == newDescription)), Times.Once);
    }

    [Test]
    public async Task UpdateProjectAsync_NonExistingProject_ThrowsArgumentException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync((Project)null!);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.UpdateProjectAsync(projectId, "New Name", "New Description", "user"));
    }

    [Test]
    public async Task GetProjectsByUserAsync_ReturnsUserProjects()
    {
        // Arrange
        var userId = "test-user";
        var expectedProjects = new List<Project>
        {
            new Project { Id = Guid.NewGuid(), Name = "Project 1", CreatedBy = "creator1", CreatedAt = DateTime.UtcNow },
            new Project { Id = Guid.NewGuid(), Name = "Project 2", CreatedBy = "creator2", CreatedAt = DateTime.UtcNow }
        };

        // Setup cache miss
        _mockCacheService.Setup(c => c.GetAsync<IEnumerable<Project>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as IEnumerable<Project>);

        _mockProjectRepository.Setup(r => r.GetProjectsByUserIdAsync(userId))
            .ReturnsAsync(expectedProjects);

        _mockCacheService.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Project>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetProjectsByUserAsync(userId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        _mockProjectRepository.Verify(r => r.GetProjectsByUserIdAsync(userId), Times.Once);
    }

    [Test]
    public async Task CanUserAccessProjectAsync_UserIsMember_ReturnsTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "test-user";

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CanUserAccessProjectAsync(projectId, userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CanUserAccessProjectAsync_UserIsNotMember_ReturnsFalse()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "test-user";

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CanUserAccessProjectAsync(projectId, userId);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task AddMemberAsync_ValidData_AddsMember()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "new-member";
        var role = ProjectRole.Member;
        var addedBy = "admin";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var expectedMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(false);

        _mockProjectRepository.Setup(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>()))
            .ReturnsAsync((ProjectMember m) => m);

        // Act
        var result = await _service.AddMemberAsync(projectId, userId, role, addedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserId, Is.EqualTo(userId));
        Assert.That(result.Role, Is.EqualTo(role));

        _mockProjectRepository.Verify(r => r.AddProjectMemberAsync(It.IsAny<ProjectMember>()), Times.Once);
    }

    [Test]
    public async Task AddMemberAsync_UserAlreadyMember_ThrowsInvalidOperationException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "existing-member";
        var role = ProjectRole.Member;
        var addedBy = "admin";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var existingMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(true);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.AddMemberAsync(projectId, userId, role, addedBy));
    }

    [Test]
    public async Task RemoveMemberAsync_ExistingMember_RemovesMember()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "member-to-remove";
        var removedBy = "admin";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(true);

        _mockProjectRepository.Setup(r => r.RemoveProjectMemberAsync(projectId, userId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RemoveMemberAsync(projectId, userId, removedBy);

        // Assert
        _mockProjectRepository.Verify(r => r.RemoveProjectMemberAsync(projectId, userId), Times.Once);
    }

    [Test]
    public async Task RemoveMemberAsync_NonExistingMember_ThrowsArgumentException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "non-existing-member";
        var removedBy = "admin";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectId))
            .ReturnsAsync(project);

        _mockProjectRepository.Setup(r => r.IsUserMemberOfProjectAsync(projectId, userId))
            .ReturnsAsync(false);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _service.RemoveMemberAsync(projectId, userId, removedBy));
    }
}