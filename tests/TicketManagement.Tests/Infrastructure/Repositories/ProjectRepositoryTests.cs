using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Infrastructure.Repositories;

namespace TicketManagement.Tests.Infrastructure.Repositories;

[TestFixture]
public class ProjectRepositoryTests
{
    private TicketDbContext _context;
    private ProjectRepository _repository;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TicketDbContext(options);
        _repository = new ProjectRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task GetByIdAsync_ExistingProject_ReturnsProject()
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

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(project.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(project.Id));
        Assert.That(result.Name, Is.EqualTo(project.Name));
    }

    [Test]
    public async Task GetByIdAsync_NonExistingProject_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistingId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllProjects()
    {
        // Arrange
        var projects = new[]
        {
            new Project { Id = Guid.NewGuid(), Name = "Project 1", CreatedBy = "user1", CreatedAt = DateTime.UtcNow },
            new Project { Id = Guid.NewGuid(), Name = "Project 2", CreatedBy = "user2", CreatedAt = DateTime.UtcNow },
            new Project { Id = Guid.NewGuid(), Name = "Project 3", CreatedBy = "user3", CreatedAt = DateTime.UtcNow }
        };

        await _context.Projects.AddRangeAsync(projects);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task AddAsync_AddsProjectToDatabase()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "New Project",
            Description = "New Description",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Act
        var result = await _repository.AddAsync(project);
        await _context.SaveChangesAsync();

        // Assert
        var savedProject = await _context.Projects.FindAsync(project.Id);
        Assert.That(savedProject, Is.Not.Null);
        Assert.That(savedProject.Name, Is.EqualTo(project.Name));
        Assert.That(result, Is.EqualTo(project));
    }

    [Test]
    public async Task UpdateAsync_UpdatesExistingProject()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original Description",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Modify project
        project.Name = "Updated Name";
        project.Description = "Updated Description";

        // Act
        var result = await _repository.UpdateAsync(project);
        await _context.SaveChangesAsync();

        // Assert
        var updatedProject = await _context.Projects.FindAsync(project.Id);
        Assert.That(updatedProject, Is.Not.Null);
        Assert.That(updatedProject.Name, Is.EqualTo("Updated Name"));
        Assert.That(updatedProject.Description, Is.EqualTo("Updated Description"));
        Assert.That(result, Is.EqualTo(project));
    }

    [Test]
    public async Task DeleteAsync_RemovesProjectFromDatabase()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project to Delete",
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(project.Id);
        await _context.SaveChangesAsync();

        // Assert
        var deletedProject = await _context.Projects.FindAsync(project.Id);
        Assert.That(deletedProject, Is.Null);
    }

    [Test]
    [Ignore("This test has issues with EF Core in-memory database navigation properties")]
    public async Task GetProjectsByUserIdAsync_ReturnsUserProjects()
    {
        // Arrange
        var userId = "test-user";
        var organizationId = Guid.NewGuid();
        var project1 = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "User Project 1",
            CreatedBy = "other-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var project2 = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "User Project 2",
            CreatedBy = "other-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var project3 = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Other Project",
            CreatedBy = "other-user",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var member1 = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project1.Id,
            UserId = userId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };
        var member2 = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project2.Id,
            UserId = userId,
            Role = ProjectRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        // First add projects
        await _context.Projects.AddRangeAsync(project1, project2, project3);
        await _context.SaveChangesAsync();
        
        // Then add members
        await _context.ProjectMembers.AddRangeAsync(member1, member2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetProjectsByUserIdAsync(userId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.Any(p => p.Id == project1.Id), Is.True);
        Assert.That(result.Any(p => p.Id == project2.Id), Is.True);
        Assert.That(result.Any(p => p.Id == project3.Id), Is.False);
    }

    [Test]
    public async Task GetProjectWithMembersAsync_ReturnsProjectWithMembers()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project with Members",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var members = new[]
        {
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = "user1",
                Role = ProjectRole.Admin,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = "user2",
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            }
        };

        await _context.Projects.AddAsync(project);
        await _context.ProjectMembers.AddRangeAsync(members);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetProjectWithMembersAsync(project.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(project.Id));
        Assert.That(result.Members.Count, Is.EqualTo(2));
        Assert.That(result.Members.Any(m => m.UserId == "user1"), Is.True);
        Assert.That(result.Members.Any(m => m.UserId == "user2"), Is.True);
    }

    [Test]
    public async Task IsUserMemberOfProjectAsync_UserIsMember_ReturnsTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "test-user";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        await _context.Projects.AddAsync(project);
        await _context.ProjectMembers.AddAsync(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsUserMemberOfProjectAsync(projectId, userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsUserMemberOfProjectAsync_UserIsNotMember_ReturnsFalse()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = "test-user";

        var project = new Project
        {
            Id = projectId,
            Name = "Test Project",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsUserMemberOfProjectAsync(projectId, userId);

        // Assert
        Assert.That(result, Is.False);
    }
}