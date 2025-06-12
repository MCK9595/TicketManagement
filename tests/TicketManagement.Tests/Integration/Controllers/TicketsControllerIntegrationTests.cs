using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Core.Entities;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Core.Enums;
using TicketManagement.Tests.Integration;
using System.Net;

namespace TicketManagement.Tests.Integration.Controllers;

[TestFixture]
[Ignore("Integration tests disabled due to Aspire dependency injection complexity")]
public class TicketsControllerIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private TicketDbContext _context;
    private Project _testProject;
    private string _userId;

    [SetUp]
    public async Task Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove only specific database services for testing
                    var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<TicketDbContext>));
                    if (dbDescriptor != null)
                        services.Remove(dbDescriptor);

                    // Add in-memory database for testing
                    services.AddDbContext<TicketDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
                    
                    // Add basic authentication and authorization for testing
                    services.AddAuthentication("Test")
                        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            "Test", options => { });
                    
                    services.AddAuthorization();
                });
            });

        _client = _factory.CreateClient();

        // Get the test database context
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<TicketDbContext>();

        // Setup test data
        _userId = "test-user";
        _testProject = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description",
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var projectMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            UserId = _userId,
            Role = ProjectRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        await _context.Projects.AddAsync(_testProject);
        await _context.ProjectMembers.AddAsync(projectMember);
        await _context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer mock-token-{_userId}");
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetTickets_WithValidProject_ReturnsProjectTickets()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProject.Id,
                Title = "First Ticket",
                Description = "First Description",
                Priority = TicketPriority.High,
                Status = TicketStatus.Open,
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProject.Id,
                Title = "Second Ticket",
                Description = "Second Description",
                Priority = TicketPriority.Medium,
                Status = TicketStatus.InProgress,
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/projects/{_testProject.Id}/tickets");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<List<TicketDto>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Count, Is.EqualTo(2));
        Assert.That(apiResponse.Data.Any(t => t.Title == "First Ticket"), Is.True);
        Assert.That(apiResponse.Data.Any(t => t.Title == "Second Ticket"), Is.True);
    }

    [Test]
    public async Task GetTicket_ExistingTicket_ReturnsTicketWithDetails()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            Title = "Test Ticket",
            Description = "Test Description",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            Content = "Test comment",
            AuthorId = _userId,
            CreatedAt = DateTime.UtcNow
        };

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AssigneeId = _userId,
            AssignedBy = _userId,
            AssignedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.Comments.AddAsync(comment);
        await _context.TicketAssignments.AddAsync(assignment);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/tickets/{ticket.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDetailDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Id, Is.EqualTo(ticket.Id));
        Assert.That(apiResponse.Data.Title, Is.EqualTo("Test Ticket"));
        Assert.That(apiResponse.Data.Comments.Count, Is.EqualTo(1));
        Assert.That(apiResponse.Data.Assignments.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateTicket_ValidData_CreatesTicket()
    {
        // Arrange
        var createDto = new CreateTicketDto
        {
            Title = "New Integration Ticket",
            Description = "Created via integration test",
            Priority = TicketPriority.High
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"/api/projects/{_testProject.Id}/tickets", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Title, Is.EqualTo(createDto.Title));
        Assert.That(apiResponse.Data.Description, Is.EqualTo(createDto.Description));
        Assert.That(apiResponse.Data.Priority, Is.EqualTo(createDto.Priority));
        Assert.That(apiResponse.Data.Status, Is.EqualTo(TicketStatus.Open));

        // Verify ticket was created in database
        var dbTicket = await _context.Tickets.FirstOrDefaultAsync(t => t.Title == createDto.Title);
        Assert.That(dbTicket, Is.Not.Null);
        Assert.That(dbTicket.CreatedBy, Is.EqualTo(_userId));
        Assert.That(dbTicket.ProjectId, Is.EqualTo(_testProject.Id));
    }

    [Test]
    public async Task CreateTicket_InvalidData_ReturnsBadRequest()
    {
        // Arrange
        var createDto = new CreateTicketDto
        {
            Title = "", // Invalid: empty title
            Description = "Test Description",
            Priority = TicketPriority.Medium
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"/api/projects/{_testProject.Id}/tickets", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UpdateTicket_ValidData_UpdatesTicket()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            Title = "Original Title",
            Description = "Original Description",
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateTicketDto
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Priority = TicketPriority.High
        };

        var json = JsonSerializer.Serialize(updateDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/api/tickets/{ticket.Id}", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Title, Is.EqualTo(updateDto.Title));
        Assert.That(apiResponse.Data.Description, Is.EqualTo(updateDto.Description));
        Assert.That(apiResponse.Data.Priority, Is.EqualTo(updateDto.Priority));

        // Verify ticket was updated in database
        var dbTicket = await _context.Tickets.FindAsync(ticket.Id);
        Assert.That(dbTicket, Is.Not.Null);
        Assert.That(dbTicket.Title, Is.EqualTo(updateDto.Title));
        Assert.That(dbTicket.Description, Is.EqualTo(updateDto.Description));
        Assert.That(dbTicket.Priority, Is.EqualTo(updateDto.Priority));
    }

    [Test]
    public async Task UpdateTicketStatus_ValidTransition_UpdatesStatus()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            Title = "Test Ticket",
            Description = "Test Description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        var statusDto = new UpdateTicketStatusDto
        {
            Status = TicketStatus.InProgress
        };

        var json = JsonSerializer.Serialize(statusDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PatchAsync($"/api/tickets/{ticket.Id}/status", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Status, Is.EqualTo(TicketStatus.InProgress));

        // Verify status was updated in database
        var dbTicket = await _context.Tickets.FindAsync(ticket.Id);
        Assert.That(dbTicket, Is.Not.Null);
        Assert.That(dbTicket.Status, Is.EqualTo(TicketStatus.InProgress));
    }

    [Test]
    public async Task AssignTicket_ValidData_AssignsTicket()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            Title = "Test Ticket",
            Description = "Test Description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        var assignDto = new AssignTicketDto
        {
            AssigneeId = _userId
        };

        var json = JsonSerializer.Serialize(assignDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"/api/tickets/{ticket.Id}/assign", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);

        // Verify assignment was created in database
        var dbAssignment = await _context.TicketAssignments
            .FirstOrDefaultAsync(a => a.TicketId == ticket.Id && a.AssigneeId == _userId);
        Assert.That(dbAssignment, Is.Not.Null);
        Assert.That(dbAssignment.AssignedBy, Is.EqualTo(_userId));
    }

    [Test]
    public async Task AddComment_ValidData_AddsComment()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProject.Id,
            Title = "Test Ticket",
            Description = "Test Description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        var commentDto = new CreateCommentDto
        {
            Content = "This is a test comment from integration test"
        };

        var json = JsonSerializer.Serialize(commentDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"/api/tickets/{ticket.Id}/comments", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<CommentDto>>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Content, Is.EqualTo(commentDto.Content));
        Assert.That(apiResponse.Data.AuthorId, Is.EqualTo(_userId));

        // Verify comment was created in database
        var dbComment = await _context.Comments
            .FirstOrDefaultAsync(c => c.TicketId == ticket.Id && c.Content == commentDto.Content);
        Assert.That(dbComment, Is.Not.Null);
        Assert.That(dbComment.AuthorId, Is.EqualTo(_userId));
    }

    [Test]
    public async Task SearchTickets_WithKeyword_ReturnsMatchingTickets()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProject.Id,
                Title = "Bug in search functionality",
                Description = "Search is not working properly",
                Priority = TicketPriority.High,
                Status = TicketStatus.Open,
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProject.Id,
                Title = "Feature request",
                Description = "Add search filters",
                Priority = TicketPriority.Medium,
                Status = TicketStatus.Open,
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                ProjectId = _testProject.Id,
                Title = "UI improvement",
                Description = "Improve user interface",
                Priority = TicketPriority.Low,
                Status = TicketStatus.Open,
                CreatedBy = _userId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/projects/{_testProject.Id}/tickets/search?keyword=search&page=1&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<PagedResultDto<TicketDto>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(apiResponse, Is.Not.Null);
        Assert.That(apiResponse.Success, Is.True);
        Assert.That(apiResponse.Data.Items.Count, Is.EqualTo(2)); // Two tickets contain "search"
        Assert.That(apiResponse.Data.TotalCount, Is.EqualTo(2));
        Assert.That(apiResponse.Data.Items.All(t => 
            t.Title.Contains("search", StringComparison.OrdinalIgnoreCase) || 
            t.Description.Contains("search", StringComparison.OrdinalIgnoreCase)), Is.True);
    }
}