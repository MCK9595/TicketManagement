using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class ProjectMemberTests
{
    private ProjectMember _projectMember;
    private Guid _projectId;

    [SetUp]
    public void Setup()
    {
        _projectId = Guid.NewGuid();
        _projectMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            UserId = "test-user",
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };
    }

    [Test]
    public void ProjectMember_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var projectMember = new ProjectMember();

        // Assert
        Assert.That(projectMember.UserId, Is.EqualTo(string.Empty));
        Assert.That(projectMember.Role, Is.EqualTo(ProjectRole.Viewer)); // Default enum value is 0 (Viewer)
    }

    [Test]
    public void ProjectMember_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_projectMember.ProjectId, Is.EqualTo(_projectId));
        Assert.That(_projectMember.UserId, Is.EqualTo("test-user"));
        Assert.That(_projectMember.Role, Is.EqualTo(ProjectRole.Member));
    }

    [Test]
    public void ProjectMember_JoinedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _projectMember.JoinedAt = expectedDate;

        // Assert
        Assert.That(_projectMember.JoinedAt, Is.EqualTo(expectedDate));
    }

    [Test]
    public void ProjectMember_Navigation_ProjectProperty()
    {
        // Arrange
        var project = new Project { Id = _projectId, Name = "Test Project", CreatedBy = "creator", CreatedAt = DateTime.UtcNow };

        // Act
        _projectMember.Project = project;

        // Assert
        Assert.That(_projectMember.Project, Is.EqualTo(project));
        Assert.That(_projectMember.Project.Id, Is.EqualTo(_projectId));
    }

    [TestCase(ProjectRole.Viewer)]
    [TestCase(ProjectRole.Member)]
    [TestCase(ProjectRole.Admin)]
    public void ProjectMember_Role_CanBeSetToValidValues(ProjectRole role)
    {
        // Act
        _projectMember.Role = role;

        // Assert
        Assert.That(_projectMember.Role, Is.EqualTo(role));
    }

    [Test]
    public void ProjectMember_UserId_CanBeEmpty()
    {
        // Act
        _projectMember.UserId = "";

        // Assert
        Assert.That(_projectMember.UserId, Is.EqualTo(""));
    }

    [Test]
    public void ProjectMember_UserId_CanBeNull()
    {
        // Act
        _projectMember.UserId = null!;

        // Assert
        Assert.That(_projectMember.UserId, Is.Null);
    }

    [Test]
    public void ProjectMember_Id_IsUniqueGuid()
    {
        // Arrange
        var member1 = new ProjectMember { Id = Guid.NewGuid() };
        var member2 = new ProjectMember { Id = Guid.NewGuid() };

        // Assert
        Assert.That(member1.Id, Is.Not.EqualTo(member2.Id));
        Assert.That(member1.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(member2.Id, Is.Not.EqualTo(Guid.Empty));
    }
}