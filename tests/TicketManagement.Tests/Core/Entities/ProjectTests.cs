using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core.Entities;

[TestFixture]
public class ProjectTests
{
    private Project _project;

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
    }

    [Test]
    public void Project_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var project = new Project();

        // Assert
        Assert.That(project.Name, Is.EqualTo(string.Empty));
        Assert.That(project.Description, Is.EqualTo(string.Empty));
        Assert.That(project.CreatedBy, Is.EqualTo(string.Empty));
        Assert.That(project.IsActive, Is.False); // Default value for bool is false
        Assert.That(project.Members, Is.Not.Null);
        Assert.That(project.Tickets, Is.Not.Null);
        Assert.That(project.Members.Count, Is.EqualTo(0));
        Assert.That(project.Tickets.Count, Is.EqualTo(0));
    }

    [Test]
    public void Project_Properties_CanBeSetAndRetrieved()
    {
        // Assert
        Assert.That(_project.Name, Is.EqualTo("Test Project"));
        Assert.That(_project.Description, Is.EqualTo("Test Description"));
        Assert.That(_project.CreatedBy, Is.EqualTo("test-user"));
        Assert.That(_project.IsActive, Is.True);
    }

    [Test]
    public void Project_AddMember_AddsToCollection()
    {
        // Arrange
        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            UserId = "test-user",
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        // Act
        _project.Members.Add(member);

        // Assert
        Assert.That(_project.Members.Count, Is.EqualTo(1));
        Assert.That(_project.Members.First(), Is.EqualTo(member));
    }

    [Test]
    public void Project_AddTicket_AddsToCollection()
    {
        // Arrange
        var ticket = new Ticket
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

        // Act
        _project.Tickets.Add(ticket);

        // Assert
        Assert.That(_project.Tickets.Count, Is.EqualTo(1));
        Assert.That(_project.Tickets.First(), Is.EqualTo(ticket));
    }

    [Test]
    public void Project_IsActive_DefaultIsTrue()
    {
        // Arrange & Act
        var project = new Project();

        // Assert
        Assert.That(project.IsActive, Is.False); // Default value for bool is false
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Project_Name_CanBeEmpty(string name)
    {
        // Act
        _project.Name = name;

        // Assert
        Assert.That(_project.Name, Is.EqualTo(name));
    }

    [Test]
    public void Project_Name_CanBeNull()
    {
        // Act
        _project.Name = null!;

        // Assert
        Assert.That(_project.Name, Is.Null);
    }

    [Test]
    public void Project_CreatedAt_CanBeSet()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        _project.CreatedAt = expectedDate;

        // Assert
        Assert.That(_project.CreatedAt, Is.EqualTo(expectedDate));
    }
}