using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using TicketManagement.Infrastructure.Services;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Infrastructure.Services;

[TestFixture]
public class OrganizationServiceTests
{
    private Mock<IOrganizationRepository> _organizationRepositoryMock;
    private Mock<IOrganizationMemberRepository> _memberRepositoryMock;
    private Mock<INotificationService> _notificationServiceMock;
    private Mock<ICacheService> _cacheServiceMock;
    private Mock<ILogger<OrganizationService>> _loggerMock;
    private OrganizationService _organizationService;

    [SetUp]
    public void Setup()
    {
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _memberRepositoryMock = new Mock<IOrganizationMemberRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<OrganizationService>>();

        _organizationService = new OrganizationService(
            _organizationRepositoryMock.Object,
            _memberRepositoryMock.Object,
            _notificationServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task CreateOrganizationAsync_ValidInput_CreatesOrganizationAndAdminMember()
    {
        // Arrange
        var name = "Test Organization";
        var displayName = "Test Org";
        var description = "Test Description";
        var createdBy = "user123";

        _organizationRepositoryMock
            .Setup(r => r.GetByNameAsync(name))
            .ReturnsAsync((Organization?)null);

        var createdOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = displayName,
            Description = description,
            CreatedBy = createdBy,
            IsActive = true
        };

        _organizationRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Organization>()))
            .ReturnsAsync(createdOrg);

        _memberRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OrganizationMember>()))
            .ReturnsAsync(new OrganizationMember());

        // Act
        var result = await _organizationService.CreateOrganizationAsync(name, displayName, description, createdBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo(name));
        Assert.That(result.DisplayName, Is.EqualTo(displayName));
        Assert.That(result.Description, Is.EqualTo(description));
        Assert.That(result.CreatedBy, Is.EqualTo(createdBy));
        Assert.That(result.IsActive, Is.True);

        _organizationRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Organization>()), Times.Once);
        _memberRepositoryMock.Verify(r => r.AddAsync(It.Is<OrganizationMember>(m => 
            m.UserId == createdBy && 
            m.Role == OrganizationRole.Admin && 
            m.IsActive)), Times.Once);
    }

    [Test]
    public async Task CreateOrganizationAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var name = "Existing Organization";
        var createdBy = "user123";

        var existingOrg = new Organization { Id = Guid.NewGuid(), Name = name };
        _organizationRepositoryMock
            .Setup(r => r.GetByNameAsync(name))
            .ReturnsAsync(existingOrg);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _organizationService.CreateOrganizationAsync(name, null, null, createdBy));

        // Verify exception is thrown
        _organizationRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Organization>()), Times.Never);
    }

    [Test]
    public async Task AddMemberAsync_ValidInput_AddsMemberAndSendsNotification()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "user123";
        var userName = "Test User";
        var userEmail = "test@example.com";
        var role = OrganizationRole.Member;
        var invitedBy = "admin123";

        _memberRepositoryMock
            .Setup(r => r.GetUserRoleInOrganizationAsync(organizationId, invitedBy))
            .ReturnsAsync(OrganizationRole.Admin);

        _organizationRepositoryMock
            .Setup(r => r.GetMemberCountAsync(organizationId))
            .ReturnsAsync(5);

        var organization = new Organization 
        { 
            Id = organizationId, 
            Name = "Test Org",
            MaxMembers = 100 
        };
        _organizationRepositoryMock
            .Setup(r => r.GetByIdAsync(organizationId))
            .ReturnsAsync(organization);

        _memberRepositoryMock
            .Setup(r => r.GetMemberAsync(organizationId, userId))
            .ReturnsAsync((OrganizationMember?)null);

        var addedMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            UserName = userName,
            UserEmail = userEmail,
            Role = role,
            InvitedBy = invitedBy,
            IsActive = true
        };

        _memberRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OrganizationMember>()))
            .ReturnsAsync(addedMember);

        // Act
        var result = await _organizationService.AddMemberAsync(organizationId, userId, userName, userEmail, role, invitedBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserId, Is.EqualTo(userId));
        Assert.That(result.Role, Is.EqualTo(role));
        Assert.That(result.IsActive, Is.True);

        _memberRepositoryMock.Verify(r => r.AddAsync(It.Is<OrganizationMember>(m =>
            m.UserId == userId &&
            m.Role == role &&
            m.InvitedBy == invitedBy)), Times.Once);

        _notificationServiceMock.Verify(n => n.CreateNotificationAsync(
            userId,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationType>(),
            It.IsAny<Guid?>()), Times.Once);
    }

    [Test]
    public async Task CanUserCreateProjectAsync_AdminUser_ReturnsTrue()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "admin123";

        _memberRepositoryMock
            .Setup(r => r.GetUserRoleInOrganizationAsync(organizationId, userId))
            .ReturnsAsync(OrganizationRole.Admin);

        // Act
        var result = await _organizationService.CanUserCreateProjectAsync(organizationId, userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CanUserCreateProjectAsync_ManagerUser_ReturnsTrue()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "manager123";

        _memberRepositoryMock
            .Setup(r => r.GetUserRoleInOrganizationAsync(organizationId, userId))
            .ReturnsAsync(OrganizationRole.Manager);

        // Act
        var result = await _organizationService.CanUserCreateProjectAsync(organizationId, userId);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CanUserCreateProjectAsync_MemberUser_ReturnsFalse()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "member123";

        _memberRepositoryMock
            .Setup(r => r.GetUserRoleInOrganizationAsync(organizationId, userId))
            .ReturnsAsync(OrganizationRole.Member);

        // Act
        var result = await _organizationService.CanUserCreateProjectAsync(organizationId, userId);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateMemberRoleAsync_LastAdminDemotion_ThrowsInvalidOperationException()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "admin123";
        var updatedBy = "admin123";
        var newRole = OrganizationRole.Member;

        _memberRepositoryMock
            .Setup(r => r.GetUserRoleInOrganizationAsync(organizationId, updatedBy))
            .ReturnsAsync(OrganizationRole.Admin);

        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = OrganizationRole.Admin,
            IsActive = true
        };

        _memberRepositoryMock
            .Setup(r => r.GetMemberAsync(organizationId, userId))
            .ReturnsAsync(member);

        _memberRepositoryMock
            .Setup(r => r.GetOrganizationAdminsAsync(organizationId))
            .ReturnsAsync(new[] { member }); // Only one admin

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _organizationService.UpdateMemberRoleAsync(organizationId, userId, newRole, updatedBy));

        // Verify exception is thrown
        _memberRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<OrganizationMember>()), Times.Never);
    }

    [Test]
    public async Task GetOrganizationAsync_WithCachedData_ReturnsCachedResult()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var cachedOrg = new Organization { Id = organizationId, Name = "Cached Org" };

        _cacheServiceMock
            .Setup(c => c.GetAsync<Organization>($"org:{organizationId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedOrg);

        // Act
        var result = await _organizationService.GetOrganizationAsync(organizationId);

        // Assert
        Assert.That(result, Is.EqualTo(cachedOrg));
        _organizationRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }
}