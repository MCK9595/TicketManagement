using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TicketManagement.ApiService.Controllers;
using TicketManagement.Contracts.Services;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Api.Controllers;

[TestFixture]
public class OrganizationsControllerTests
{
    private Mock<IOrganizationService> _organizationServiceMock;
    private Mock<IProjectService> _projectServiceMock;
    private Mock<IUserManagementService> _userManagementServiceMock;
    private Mock<ILogger<OrganizationsController>> _loggerMock;
    private OrganizationsController _controller;
    private ClaimsPrincipal _user;

    [SetUp]
    public void Setup()
    {
        _organizationServiceMock = new Mock<IOrganizationService>();
        _projectServiceMock = new Mock<IProjectService>();
        _userManagementServiceMock = new Mock<IUserManagementService>();
        _loggerMock = new Mock<ILogger<OrganizationsController>>();

        _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("sub", "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User")
        }));

        _controller = new OrganizationsController(
            _organizationServiceMock.Object,
            _projectServiceMock.Object,
            _userManagementServiceMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = _user }
            }
        };
    }

    [Test]
    public async Task GetUserOrganizations_ReturnsUserOrganizations()
    {
        // Arrange
        var organizations = new List<Organization>
        {
            new() { Id = Guid.NewGuid(), Name = "Org 1", DisplayName = "Organization 1" },
            new() { Id = Guid.NewGuid(), Name = "Org 2", DisplayName = "Organization 2" }
        };

        _organizationServiceMock
            .Setup(s => s.GetUserOrganizationsAsync("test-user-id"))
            .ReturnsAsync(organizations);

        // Act
        var result = await _controller.GetUserOrganizations();

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<List<OrganizationDto>>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.Count, Is.EqualTo(2));
        Assert.That(response.Data.First().Name, Is.EqualTo("Org 1"));
    }

    [Test]
    public async Task CreateOrganization_ValidInput_ReturnsCreatedOrganization()
    {
        // Arrange
        var createDto = new CreateOrganizationDto
        {
            Name = "New Organization",
            DisplayName = "New Org",
            Description = "Test description"
        };

        var createdOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = createDto.Name,
            DisplayName = createDto.DisplayName,
            Description = createDto.Description,
            CreatedBy = "test-user-id",
            IsActive = true
        };

        _organizationServiceMock
            .Setup(s => s.CreateOrganizationAsync(
                createDto.Name,
                createDto.DisplayName,
                createDto.Description,
                "test-user-id"))
            .ReturnsAsync(createdOrg);

        // Act
        var result = await _controller.CreateOrganization(createDto);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<CreatedAtActionResult>());
        var createdResult = result.Result as CreatedAtActionResult;
        var response = createdResult?.Value as ApiResponseDto<OrganizationDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.Name, Is.EqualTo(createDto.Name));
        Assert.That(response.Data.DisplayName, Is.EqualTo(createDto.DisplayName));
    }

    [Test]
    public async Task CreateOrganization_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        var createDto = new CreateOrganizationDto(); // Invalid - no name
        _controller.ModelState.AddModelError("Name", "Name is required");

        // Act
        var result = await _controller.CreateOrganization(createDto);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result.Result as BadRequestObjectResult;
        var response = badRequestResult?.Value as ApiResponseDto<OrganizationDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.False);
    }

    [Test]
    public async Task GetOrganization_ExistingId_ReturnsOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            DisplayName = "Test Org",
            IsActive = true,
            Members = new List<OrganizationMember>()
        };

        _organizationServiceMock
            .Setup(s => s.CanUserAccessOrganizationAsync(organizationId, "test-user-id"))
            .ReturnsAsync(true);

        _organizationServiceMock
            .Setup(s => s.GetOrganizationWithDetailsAsync(organizationId))
            .ReturnsAsync(organization);

        _organizationServiceMock
            .Setup(s => s.GetProjectLimitsAsync(organizationId))
            .ReturnsAsync((0, 10));

        _organizationServiceMock
            .Setup(s => s.GetMemberLimitsAsync(organizationId))
            .ReturnsAsync((1, 50));

        // Act
        var result = await _controller.GetOrganization(organizationId);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<OrganizationDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.Id, Is.EqualTo(organizationId));
        Assert.That(response.Data.Name, Is.EqualTo("Test Organization"));
    }

    [Test]
    public async Task GetOrganization_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        
        _organizationServiceMock
            .Setup(s => s.CanUserAccessOrganizationAsync(organizationId, "test-user-id"))
            .ReturnsAsync(true);

        _organizationServiceMock
            .Setup(s => s.GetOrganizationWithDetailsAsync(organizationId))
            .ReturnsAsync((Organization?)null);

        // Act
        var result = await _controller.GetOrganization(organizationId);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task AddMember_ValidInput_ReturnsAddedMember()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var addMemberDto = new AddOrganizationMemberDto
        {
            UserId = "new-user-id",
            UserName = "New User",
            UserEmail = "newuser@example.com",
            Role = OrganizationRole.Member
        };

        var addedMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = addMemberDto.UserId,
            UserName = addMemberDto.UserName,
            UserEmail = addMemberDto.UserEmail,
            Role = addMemberDto.Role,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _organizationServiceMock
            .Setup(s => s.AddMemberAsync(
                organizationId,
                addMemberDto.UserId,
                addMemberDto.UserName,
                addMemberDto.UserEmail,
                addMemberDto.Role,
                "test-user-id"))
            .ReturnsAsync(addedMember);

        // Act
        var result = await _controller.AddOrganizationMember(organizationId, addMemberDto);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<OrganizationMemberDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.UserId, Is.EqualTo(addMemberDto.UserId));
        Assert.That(response.Data.Role, Is.EqualTo(addMemberDto.Role));
    }

    [Test]
    public async Task UpdateMemberRole_ValidInput_ReturnsUpdatedMember()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "member-user-id";
        var updateDto = new UpdateOrganizationMemberDto
        {
            Role = OrganizationRole.Manager
        };

        var updatedMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            UserName = "Member User",
            Role = updateDto.Role,
            IsActive = true
        };

        _organizationServiceMock
            .Setup(s => s.UpdateMemberRoleAsync(organizationId, userId, updateDto.Role, "test-user-id"))
            .ReturnsAsync(updatedMember);

        // Act
        var result = await _controller.UpdateMemberRole(organizationId, userId, updateDto);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<OrganizationMemberDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.UserId, Is.EqualTo(userId));
        Assert.That(response.Data.Role, Is.EqualTo(updateDto.Role));
    }

    [Test]
    public async Task RemoveMember_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = "member-to-remove";

        _organizationServiceMock
            .Setup(s => s.RemoveMemberAsync(organizationId, userId, "test-user-id"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveOrganizationMember(organizationId, userId);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<string>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
    }

    [Test]
    public async Task CreateProjectInOrganization_ValidInput_ReturnsCreatedProject()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var createDto = new CreateProjectDto
        {
            OrganizationId = organizationId,
            Name = "Test Project",
            Description = "Test project description"
        };

        var createdProject = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = createDto.Name,
            Description = createDto.Description,
            CreatedBy = "test-user-id",
            IsActive = true
        };

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization"
        };

        _projectServiceMock
            .Setup(s => s.CreateProjectAsync(organizationId, createDto.Name, createDto.Description, "test-user-id"))
            .ReturnsAsync(createdProject);

        _organizationServiceMock
            .Setup(s => s.GetOrganizationAsync(organizationId))
            .ReturnsAsync(organization);

        // Act
        var result = await _controller.CreateProjectInOrganization(organizationId, createDto);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var response = okResult?.Value as ApiResponseDto<ProjectDto>;
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data.Name, Is.EqualTo(createDto.Name));
        Assert.That(response.Data.OrganizationId, Is.EqualTo(organizationId));
    }
}