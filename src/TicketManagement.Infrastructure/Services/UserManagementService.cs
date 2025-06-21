using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TicketManagement.Contracts.Services;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Infrastructure.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace TicketManagement.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly HttpClient _httpClient;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _organizationMemberRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TicketDbContext _context;
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
        TicketDbContext context,
        ICacheService cacheService,
        ILogger<UserManagementService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _organizationRepository = organizationRepository;
        _organizationMemberRepository = organizationMemberRepository;
        _context = context;
        _projectRepository = projectRepository;
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
        _logger = logger;
        _configuration = configuration;

        // Configure HttpClient for Keycloak API
        // Get Keycloak URL from connection string (Aspire service discovery) or configuration
        var keycloakBaseUrl = GetKeycloakBaseUrl();
        var realm = _configuration["Keycloak:Realm"] ?? "ticket-management";
        
        _logger.LogInformation("Configuring UserManagementService with Keycloak URL: {KeycloakUrl}", keycloakBaseUrl);
        _httpClient.BaseAddress = new Uri($"{keycloakBaseUrl}/admin/realms/{realm}/");
        
        // Set timeout for Keycloak operations
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private async Task<string?> GetServiceAccountTokenAsync()
    {
        try
        {
            // Check if we have a cached token that's still valid
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
            {
                return _cachedToken;
            }

            // Use client credentials flow for service account
            var clientId = _configuration["Keycloak:AdminClientId"] ?? "ticket-management-service";
            var clientSecret = _configuration["Keycloak:AdminClientSecret"] ?? "ticket-management-service-secret";

            // Get Keycloak URL from connection string (Aspire service discovery) or configuration
            var keycloakBaseUrl = GetKeycloakBaseUrl();
            var realm = _configuration["Keycloak:Realm"] ?? "ticket-management";
            
            var formData = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", clientId),
                new("client_secret", clientSecret)
            };

            // Create a new HttpClient for token requests to avoid header conflicts
            using var tokenClient = new HttpClient();
            tokenClient.DefaultRequestHeaders.Accept.Clear();
            tokenClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var tokenUrl = $"{keycloakBaseUrl}/realms/{realm}/protocol/openid-connect/token";
            _logger.LogDebug("Requesting service account token from: {TokenUrl}", tokenUrl);
            _logger.LogDebug("Using client credentials: ClientId={ClientId}", clientId);
            
            // Set timeout for token requests
            tokenClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await tokenClient.PostAsync(tokenUrl, new FormUrlEncodedContent(formData));
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Token request response: StatusCode={StatusCode}, IsSuccessStatusCode={IsSuccessStatusCode}", 
                response.StatusCode, response.IsSuccessStatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully received service account token from Keycloak");
                var tokenResponse = JsonSerializer.Deserialize<TokenResponseDto>(content, _jsonOptions);
                
                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    _cachedToken = tokenResponse.AccessToken;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    _logger.LogDebug("Token cached successfully, expires at: {ExpiryTime}", _tokenExpiry);
                    return _cachedToken;
                }
                else
                {
                    _logger.LogError("Token response parsing failed: AccessToken is null or empty");
                }
            }
            else
            {
                _logger.LogError("Failed to get service account token: {StatusCode}, Response: {ErrorContent}", 
                    response.StatusCode, content);
            }
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
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    // 新しいユーザー管理機能の実装

    public async Task<CreateUserResult> CreateUserAsync(CreateUserDto createUserDto)
    {
        try
        {
            _logger.LogInformation("Starting user creation for username: {Username}", createUserDto.Username);
            
            var token = await GetServiceAccountTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to obtain service account token for user creation");
                return new CreateUserResult
                {
                    Success = false,
                    Errors = new List<string> { "Failed to authenticate with identity provider" },
                    Message = "Authentication failed with Keycloak"
                };
            }
            
            _logger.LogDebug("Successfully obtained service account token for user creation");

            var temporaryPassword = !string.IsNullOrEmpty(createUserDto.TemporaryPassword)
                ? createUserDto.TemporaryPassword
                : PasswordManager.GenerateTemporaryPassword();

            var keycloakUser = new
            {
                username = createUserDto.Username,
                email = createUserDto.Email,
                firstName = createUserDto.FirstName,
                lastName = createUserDto.LastName,
                enabled = createUserDto.IsActive,
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = temporaryPassword,
                        temporary = createUserDto.RequirePasswordChange
                    }
                }
            };

            var json = JsonSerializer.Serialize(keycloakUser, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            // Create user using the configured base path
            var adminUrl = "users";
            
            _logger.LogDebug("Sending user creation request to Keycloak: {Url}", adminUrl);
            _logger.LogDebug("User data: {UserData}", JsonSerializer.Serialize(keycloakUser, _jsonOptions));
            
            var response = await _httpClient.PostAsync(adminUrl, content);
            
            _logger.LogInformation("Keycloak user creation response: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var locationHeader = response.Headers.Location?.ToString();
                _logger.LogDebug("Location header from Keycloak: {LocationHeader}", locationHeader);
                
                var userId = locationHeader?.Split('/').LastOrDefault();
                _logger.LogDebug("Extracted user ID: {UserId}", userId);

                if (!string.IsNullOrEmpty(userId))
                {
                    _logger.LogInformation("User created successfully in Keycloak with ID: {UserId}", userId);
                    
                    // 組織への追加
                    foreach (var roleAssignment in createUserDto.RoleAssignments)
                    {
                        _logger.LogDebug("Adding user {UserId} to organization {OrganizationId} with role {Role}", 
                            userId, roleAssignment.OrganizationId, roleAssignment.Role);
                        await AddUserToOrganizationAsync(userId, roleAssignment.OrganizationId, roleAssignment.Role);
                    }

                    _logger.LogInformation("User creation process completed successfully for {Username}", createUserDto.Username);
                    return new CreateUserResult
                    {
                        Success = true,
                        UserId = userId,
                        TemporaryPassword = temporaryPassword,
                        Message = "User created successfully"
                    };
                }
                else
                {
                    _logger.LogError("Failed to extract user ID from location header: {LocationHeader}", locationHeader);
                }
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            _logger.LogError("Keycloak user creation failed: {StatusCode}, Response: {ErrorMessage}", 
                response.StatusCode, errorMessage);
                
            return new CreateUserResult
            {
                Success = false,
                Errors = new List<string> { $"Failed to create user: {response.StatusCode} - {errorMessage}" },
                Message = $"Keycloak API error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", createUserDto.Username);
            return new CreateUserResult
            {
                Success = false,
                Errors = new List<string> { "An unexpected error occurred while creating the user" }
            };
        }
    }

    public async Task<InviteUserResult> InviteUserToOrganizationAsync(InviteUserDto inviteDto)
    {
        try
        {
            // 既存ユーザーを検索
            var existingUsers = await SearchUsersAsync(inviteDto.Email, 1);
            var existingUser = existingUsers.FirstOrDefault();

            if (existingUser != null)
            {
                // 既存ユーザーを組織に追加
                var success = await AddUserToOrganizationAsync(existingUser.Id, inviteDto.OrganizationId, inviteDto.Role);
                
                return new InviteUserResult
                {
                    Success = success,
                    UserId = existingUser.Id,
                    UserAlreadyExists = true,
                    Message = success ? "User added to organization successfully" : "Failed to add user to organization"
                };
            }
            else
            {
                // 新規ユーザーを作成
                var createUserDto = new CreateUserDto
                {
                    Username = inviteDto.Email,
                    Email = inviteDto.Email,
                    FirstName = inviteDto.FirstName,
                    LastName = inviteDto.LastName,
                    IsActive = true,
                    RequirePasswordChange = true,
                    RoleAssignments = new List<UserRoleAssignmentDto>
                    {
                        new()
                        {
                            OrganizationId = inviteDto.OrganizationId,
                            Role = inviteDto.Role
                        }
                    }
                };

                var createResult = await CreateUserAsync(createUserDto);
                
                return new InviteUserResult
                {
                    Success = createResult.Success,
                    UserId = createResult.UserId,
                    UserAlreadyExists = false,
                    Errors = createResult.Errors,
                    Message = createResult.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user {Email} to organization {OrganizationId}", 
                inviteDto.Email, inviteDto.OrganizationId);
            return new InviteUserResult
            {
                Success = false,
                Errors = new List<string> { "An unexpected error occurred while inviting the user" }
            };
        }
    }

    public async Task<ResetPasswordResult> ResetUserPasswordAsync(string userId, bool temporary = true)
    {
        try
        {
            var token = await GetServiceAccountTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    Errors = new List<string> { "Failed to authenticate with identity provider" }
                };
            }

            var newPassword = PasswordManager.GenerateTemporaryPassword();
            var credential = new
            {
                type = "password",
                value = newPassword,
                temporary = temporary
            };

            var json = JsonSerializer.Serialize(credential, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.PutAsync($"users/{userId}/reset-password", content);

            if (response.IsSuccessStatusCode)
            {
                return new ResetPasswordResult
                {
                    Success = true,
                    TemporaryPassword = newPassword,
                    Message = "Password reset successfully"
                };
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return new ResetPasswordResult
            {
                Success = false,
                Errors = new List<string> { $"Failed to reset password: {errorMessage}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return new ResetPasswordResult
            {
                Success = false,
                Errors = new List<string> { "An unexpected error occurred while resetting the password" }
            };
        }
    }

    public async Task<bool> UpdateUserRoleAsync(Guid organizationId, string userId, OrganizationRole newRole)
    {
        try
        {
            var existingMember = await _organizationMemberRepository.GetByUserIdAndOrganizationIdAsync(userId, organizationId);
            if (existingMember == null)
            {
                return false;
            }

            existingMember.Role = newRole;

            await _organizationMemberRepository.UpdateAsync(existingMember);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role for {UserId} in organization {OrganizationId}", 
                userId, organizationId);
            return false;
        }
    }

    public async Task<bool> GrantSystemAdminAsync(GrantSystemAdminDto grantDto, string grantedBy)
    {
        try
        {
            var existingAdmin = await _context.SystemAdmins
                .FirstOrDefaultAsync(sa => sa.UserId == grantDto.UserId);

            if (existingAdmin != null)
            {
                if (existingAdmin.IsActive)
                {
                    return true; // Already an active system admin
                }
                
                // Reactivate
                existingAdmin.IsActive = true;
                existingAdmin.GrantedAt = DateTime.UtcNow;
                existingAdmin.GrantedBy = grantedBy;
                existingAdmin.Reason = grantDto.Reason;
            }
            else
            {
                var systemAdmin = new SystemAdmin
                {
                    Id = Guid.NewGuid(),
                    UserId = grantDto.UserId,
                    UserName = grantDto.UserName,
                    UserEmail = grantDto.UserEmail,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy,
                    IsActive = true,
                    Reason = grantDto.Reason
                };

                _context.SystemAdmins.Add(systemAdmin);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting system admin privileges to user {UserId}", grantDto.UserId);
            return false;
        }
    }

    public async Task<bool> RevokeSystemAdminAsync(string userId, string revokedBy)
    {
        try
        {
            var systemAdmin = await _context.SystemAdmins
                .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.IsActive);

            if (systemAdmin == null)
            {
                return false;
            }

            systemAdmin.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking system admin privileges from user {UserId}", userId);
            return false;
        }
    }

    public async Task<IEnumerable<SystemAdminDto>> GetSystemAdminsAsync()
    {
        try
        {
            var systemAdmins = await _context.SystemAdmins
                .Where(sa => sa.IsActive)
                .ToListAsync();

            return systemAdmins.Select(sa => new SystemAdminDto
            {
                Id = sa.Id,
                UserId = sa.UserId,
                UserName = sa.UserName,
                UserEmail = sa.UserEmail,
                GrantedAt = sa.GrantedAt,
                GrantedBy = sa.GrantedBy,
                IsActive = sa.IsActive,
                Reason = sa.Reason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system admins");
            return Enumerable.Empty<SystemAdminDto>();
        }
    }

    public async Task<bool> IsSystemAdminAsync(string userId)
    {
        try
        {
            return await _context.SystemAdmins
                .AnyAsync(sa => sa.UserId == userId && sa.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is system admin", userId);
            return false;
        }
    }

    public async Task<bool> IsOrganizationAdminAsync(string userId, Guid? organizationId = null)
    {
        try
        {
            var query = _context.OrganizationMembers
                .Where(om => om.UserId == userId && om.Role == OrganizationRole.Admin);

            if (organizationId.HasValue)
            {
                query = query.Where(om => om.OrganizationId == organizationId.Value);
            }

            return await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is organization admin", userId);
            return false;
        }
    }

    private async Task<bool> AddUserToOrganizationAsync(string userId, Guid organizationId, OrganizationRole role)
    {
        try
        {
            var existingMember = await _organizationMemberRepository.GetByUserIdAndOrganizationIdAsync(userId, organizationId);
            if (existingMember != null)
            {
                return true; // Already a member
            }

            // Fetch user details from Keycloak to populate member information
            UserDto? userInfo = null;
            try
            {
                userInfo = await GetUserByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch user details from Keycloak for user {UserId}, using fallback values", userId);
            }

            var member = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                UserName = userInfo?.DisplayName ?? userInfo?.Username ?? $"User-{userId[..Math.Min(8, userId.Length)]}",
                UserEmail = userInfo?.Email,
                Role = role,
                JoinedAt = DateTime.UtcNow
            };

            await _organizationMemberRepository.AddAsync(member);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to organization {OrganizationId}", userId, organizationId);
            return false;
        }
    }
    
    /// <summary>
    /// Synchronizes existing organization members with Keycloak user information
    /// </summary>
    public async Task SyncOrganizationMembersAsync(Guid organizationId)
    {
        try
        {
            var members = await _organizationMemberRepository.GetOrganizationMembersAsync(organizationId);
            // Update members with missing info or where UserName looks like a UserId (UUID pattern)
            var uuidPattern = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";
            var membersToUpdate = members.Where(m => 
                string.IsNullOrEmpty(m.UserName) || 
                string.IsNullOrEmpty(m.UserEmail) ||
                (m.UserName == m.UserId) ||
                System.Text.RegularExpressions.Regex.IsMatch(m.UserName ?? "", uuidPattern)
            ).ToList();
            
            if (!membersToUpdate.Any())
            {
                _logger.LogDebug("No members need synchronization for organization {OrganizationId}", organizationId);
                return;
            }

            _logger.LogInformation("Synchronizing {Count} members for organization {OrganizationId}", membersToUpdate.Count, organizationId);

            foreach (var member in membersToUpdate)
            {
                try
                {
                    var userInfo = await GetUserByIdAsync(member.UserId);
                    if (userInfo != null)
                    {
                        member.UserName = userInfo.DisplayName ?? userInfo.Username ?? member.UserName ?? $"User-{member.UserId[..Math.Min(8, member.UserId.Length)]}";
                        member.UserEmail = userInfo.Email ?? member.UserEmail;
                        
                        await _organizationMemberRepository.UpdateAsync(member);
                        _logger.LogDebug("Updated member {UserId} in organization {OrganizationId}", member.UserId, organizationId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not sync member {UserId} in organization {OrganizationId}", member.UserId, organizationId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing members for organization {OrganizationId}", organizationId);
        }
    }
    
    /// <summary>
    /// Gets the Keycloak base URL with proper fallback logic for Aspire service discovery
    /// </summary>
    private string GetKeycloakBaseUrl()
    {
        // Try Aspire service discovery connection string first
        var connectionString = _configuration.GetConnectionString("keycloak");
        if (!string.IsNullOrEmpty(connectionString))
        {
            _logger.LogDebug("Using Keycloak connection string from Aspire: {ConnectionString}", connectionString);
            return connectionString.TrimEnd('/');
        }
        
        // Try explicit authority configuration
        var authority = _configuration["Keycloak:Authority"];
        if (!string.IsNullOrEmpty(authority))
        {
            _logger.LogDebug("Using Keycloak authority from configuration: {Authority}", authority);
            return authority.TrimEnd('/');
        }
        
        // Try base URL configuration
        var baseUrl = _configuration["Authentication:Keycloak:BaseUrl"];
        if (!string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogDebug("Using Keycloak base URL from configuration: {BaseUrl}", baseUrl);
            return baseUrl.TrimEnd('/');
        }
        
        // Final fallback - use localhost for development
        var fallbackUrl = "http://localhost:8080";
        _logger.LogWarning("No Keycloak configuration found, using fallback URL: {FallbackUrl}", fallbackUrl);
        return fallbackUrl;
    }
}