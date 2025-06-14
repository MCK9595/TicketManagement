using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TicketManagement.Contracts.Services;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using Microsoft.Extensions.Configuration;

namespace TicketManagement.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly HttpClient _httpClient;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _organizationMemberRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UserManagementService> _logger;
    private readonly IConfiguration _configuration;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public UserManagementService(
        HttpClient httpClient,
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository organizationMemberRepository,
        IProjectRepository projectRepository,
        IHttpContextAccessor httpContextAccessor,
        ICacheService cacheService,
        ILogger<UserManagementService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _organizationRepository = organizationRepository;
        _organizationMemberRepository = organizationMemberRepository;
        _projectRepository = projectRepository;
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
        _logger = logger;
        _configuration = configuration;

        // Configure HttpClient for Keycloak API
        // Use Aspire service discovery URL with port
        var keycloakBaseUrl = "http://keycloak:8080";
        var realm = _configuration["Keycloak:Realm"] ?? "ticket-management";
        
        _httpClient.BaseAddress = new Uri($"{keycloakBaseUrl}/admin/realms/{realm}/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GetUserByIdAsync called with null or empty userId");
            return null;
        }

        try
        {
            var cacheKey = $"user:{userId}";
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            await EnsureAuthTokenAsync();

            var url = $"users/{userId}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user {UserId} from Keycloak: {StatusCode}", userId, response.StatusCode);
                
                // Fallback: Create a basic user from current context if available
                var fallbackUser = CreateFallbackUser(userId);
                if (fallbackUser != null)
                {
                    await _cacheService.SetAsync(cacheKey, fallbackUser, TimeSpan.FromMinutes(5));
                    return fallbackUser;
                }
                
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var keycloakUser = JsonSerializer.Deserialize<KeycloakUserDto>(content, _jsonOptions);

            if (keycloakUser == null)
            {
                _logger.LogWarning("Failed to deserialize Keycloak user response");
                
                // Fallback: Create a basic user from current context if available
                var fallbackUser = CreateFallbackUser(userId);
                if (fallbackUser != null)
                {
                    await _cacheService.SetAsync(cacheKey, fallbackUser, TimeSpan.FromMinutes(5));
                    return fallbackUser;
                }
                
                return null;
            }

            var userDto = MapKeycloakUserToUserDto(keycloakUser);
            
            // Cache for 15 minutes
            await _cacheService.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(15));

            return userDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId} from Keycloak", userId);
            
            // Fallback: Create a basic user from current context if available
            var fallbackUser = CreateFallbackUser(userId);
            if (fallbackUser != null)
            {
                var cacheKey = $"user:{userId}";
                await _cacheService.SetAsync(cacheKey, fallbackUser, TimeSpan.FromMinutes(5));
                return fallbackUser;
            }
            
            return null;
        }
    }

    public async Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<UserDto>();

        try
        {
            var cacheKey = $"user-search:{searchTerm}:{maxResults}";
            var cached = await _cacheService.GetAsync<List<UserDto>>(cacheKey);
            if (cached != null)
                return cached;

            await EnsureAuthTokenAsync();

            var query = $"users?search={Uri.EscapeDataString(searchTerm)}&max={maxResults}";
            var response = await _httpClient.GetAsync(query);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to search users in Keycloak: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<UserDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var keycloakUsers = JsonSerializer.Deserialize<KeycloakUserDto[]>(content, _jsonOptions);

            if (keycloakUsers == null)
                return Enumerable.Empty<UserDto>();

            var users = keycloakUsers.Select(MapKeycloakUserToUserDto).ToList();
            
            // Cache for 5 minutes (shorter for search results)
            await _cacheService.SetAsync(cacheKey, users, TimeSpan.FromMinutes(5));

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users in Keycloak with term: {SearchTerm}", searchTerm);
            return Enumerable.Empty<UserDto>();
        }
    }

    public async Task<Dictionary<string, UserDto>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        var result = new Dictionary<string, UserDto>();
        var uncachedUserIds = new List<string>();

        // Check cache first
        foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)))
        {
            var cacheKey = $"user:{userId}";
            var cached = await _cacheService.GetAsync<UserDto>(cacheKey);
            if (cached != null)
            {
                result[userId] = cached;
            }
            else
            {
                uncachedUserIds.Add(userId);
            }
        }

        // Fetch uncached users
        foreach (var userId in uncachedUserIds)
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                result[userId] = user;
            }
        }

        return result;
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return null;

        return await GetUserByIdAsync(userId);
    }

    public async Task<UserDetailDto?> GetUserDetailAsync(string userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return null;

        try
        {
            var cacheKey = $"user-detail:{userId}";
            var cached = await _cacheService.GetAsync<UserDetailDto>(cacheKey);
            if (cached != null)
                return cached;

            // Get user's organizations with their roles
            _logger.LogInformation("Fetching organization memberships for userId: {UserId} (Length: {Length})", userId, userId?.Length ?? 0);
            var organizationMembers = await _organizationMemberRepository.GetUserOrganizationMembershipsAsync(userId);
            _logger.LogInformation("Found {Count} organization memberships for user {UserId}", organizationMembers.Count(), userId);
            
            foreach (var membership in organizationMembers)
            {
                _logger.LogInformation("Found membership: OrgId={OrgId}, UserId={UserId}, Role={Role}, IsActive={IsActive}", 
                    membership.OrganizationId, membership.UserId, membership.Role, membership.IsActive);
            }
            
            // Get user's projects
            var projects = await _projectRepository.GetProjectsByUserIdAsync(userId);
            var projectMembers = new List<ProjectMember>();
            
            foreach (var project in projects)
            {
                var projectMembersForProject = await _projectRepository.GetProjectMembersAsync(project.Id);
                var userMember = projectMembersForProject.FirstOrDefault(pm => pm.UserId == userId);
                if (userMember != null)
                {
                    projectMembers.Add(userMember);
                }
            }

            var userDetail = new UserDetailDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin,
                Organizations = organizationMembers.Select(o => new UserOrganizationDto
                {
                    OrganizationId = o.OrganizationId,
                    OrganizationName = o.Organization?.Name ?? string.Empty,
                    OrganizationDisplayName = o.Organization?.DisplayName,
                    Role = o.Role,
                    JoinedAt = o.JoinedAt,
                    IsActive = o.IsActive
                }).ToList(),
                Projects = projectMembers.Select(pm => new UserProjectDto
                {
                    ProjectId = pm.ProjectId,
                    ProjectName = pm.Project?.Name ?? string.Empty,
                    OrganizationId = pm.Project?.OrganizationId ?? Guid.Empty,
                    OrganizationName = pm.Project?.Organization?.Name ?? string.Empty,
                    Role = pm.Role,
                    JoinedAt = pm.JoinedAt,
                    IsActive = true
                }).ToList()
            };

            // Cache for 10 minutes
            await _cacheService.SetAsync(cacheKey, userDetail, TimeSpan.FromMinutes(10));

            return userDetail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user detail for {UserId}", userId);
            return null;
        }
    }

    public async Task<IEnumerable<UserOrganizationDto>> GetUserOrganizationsAsync(string userId)
    {
        try
        {
            var organizationMembers = await _organizationMemberRepository.GetUserOrganizationMembershipsAsync(userId);
            return organizationMembers.Select(o => new UserOrganizationDto
            {
                OrganizationId = o.OrganizationId,
                OrganizationName = o.Organization?.Name ?? string.Empty,
                OrganizationDisplayName = o.Organization?.DisplayName,
                Role = o.Role,
                JoinedAt = o.JoinedAt,
                IsActive = o.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations for user {UserId}", userId);
            return Enumerable.Empty<UserOrganizationDto>();
        }
    }

    public async Task<bool> IsUserActiveAsync(string userId)
    {
        var user = await GetUserByIdAsync(userId);
        return user?.IsActive ?? false;
    }

    private async Task EnsureAuthTokenAsync()
    {
        // In a production environment, you would implement proper service account authentication
        // For now, we'll use the admin token or service account token
        var token = await GetServiceAccountTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<string?> GetServiceAccountTokenAsync()
    {
        try
        {
            // This would typically use client credentials flow
            // For development, you might use admin credentials
            var clientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
            var clientSecret = _configuration["Keycloak:AdminClientSecret"];
            var username = _configuration["Keycloak:AdminUsername"] ?? "admin";
            var password = _configuration["Keycloak:AdminPassword"] ?? "admin123";

            // Use Aspire service discovery URL with port
            var keycloakBaseUrl = "http://keycloak:8080";
            var realm = _configuration["Keycloak:Realm"] ?? "ticket-management";
            
            var formData = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "password"),
                new("client_id", clientId),
                new("username", username),
                new("password", password)
            };

            if (!string.IsNullOrEmpty(clientSecret))
            {
                formData.Add(new("client_secret", clientSecret));
            }

            // Temporarily clear the Authorization header to avoid token conflicts
            var originalAuth = _httpClient.DefaultRequestHeaders.Authorization;
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var tokenUrl = $"{keycloakBaseUrl}/realms/{realm}/protocol/openid-connect/token";
            var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(formData));

            // Restore the original Authorization header
            _httpClient.DefaultRequestHeaders.Authorization = originalAuth;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponseDto>(content, _jsonOptions);
                return tokenResponse?.AccessToken;
            }

            _logger.LogWarning("Failed to get service account token: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service account token");
            return null;
        }
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
    }

    private UserDto? CreateFallbackUser(string userId)
    {
        try
        {
            // Check if this is the current user and we can get info from JWT claims
            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId && _httpContextAccessor.HttpContext?.User != null)
            {
                var user = _httpContextAccessor.HttpContext.User;
                var displayName = user.FindFirst("name")?.Value ??
                                user.FindFirst("preferred_username")?.Value ??
                                user.FindFirst("given_name")?.Value ??
                                user.FindFirst("email")?.Value ??
                                user.Identity?.Name ??
                                $"User-{userId.Substring(0, Math.Min(8, userId.Length))}";

                var username = user.FindFirst("preferred_username")?.Value ??
                             user.FindFirst("email")?.Value ??
                             user.Identity?.Name ??
                             $"user-{userId.Substring(0, Math.Min(8, userId.Length))}";

                return new UserDto
                {
                    Id = userId,
                    Username = username,
                    Email = user.FindFirst("email")?.Value ?? "",
                    FirstName = user.FindFirst("given_name")?.Value ?? "",
                    LastName = user.FindFirst("family_name")?.Value ?? "",
                    DisplayName = displayName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
            }

            // For other users, create a basic fallback
            return new UserDto
            {
                Id = userId,
                Username = $"user-{userId.Substring(0, Math.Min(8, userId.Length))}",
                Email = "",
                FirstName = "",
                LastName = "",
                DisplayName = $"User-{userId.Substring(0, Math.Min(8, userId.Length))}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fallback user for {UserId}", userId);
            return null;
        }
    }

    private static UserDto MapKeycloakUserToUserDto(KeycloakUserDto keycloakUser)
    {
        return new UserDto
        {
            Id = keycloakUser.Id,
            Username = keycloakUser.Username,
            Email = keycloakUser.Email,
            FirstName = keycloakUser.FirstName,
            LastName = keycloakUser.LastName,
            DisplayName = !string.IsNullOrEmpty(keycloakUser.FirstName) || !string.IsNullOrEmpty(keycloakUser.LastName)
                ? $"{keycloakUser.FirstName} {keycloakUser.LastName}".Trim()
                : keycloakUser.Username,
            IsActive = keycloakUser.Enabled,
            CreatedAt = keycloakUser.CreatedTimestamp.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(keycloakUser.CreatedTimestamp.Value).DateTime
                : null
        };
    }

    // DTOs for Keycloak API responses
    private class KeycloakUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; } = true;
        public long? CreatedTimestamp { get; set; }
    }

    private class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}