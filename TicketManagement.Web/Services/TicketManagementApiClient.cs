using TicketManagement.Contracts.DTOs;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TicketManagement.Web.Services;

public class TicketManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<TicketManagementApiClient> _logger;

    public TicketManagementApiClient(HttpClient httpClient, ILogger<TicketManagementApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    // Projects API
    public async Task<ApiResponseDto<List<ProjectDto>>?> GetProjectsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/projects");
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Get projects response. Status: {StatusCode}, Content-Type: {ContentType}", 
                response.StatusCode, response.Content.Headers.ContentType);
            
            if (response.IsSuccessStatusCode)
            {
                // Check if content is empty or not JSON
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Response content is empty");
                    return new ApiResponseDto<List<ProjectDto>> 
                    { 
                        Success = false, 
                        Message = "Empty response from server",
                        Data = new List<ProjectDto>()
                    };
                }
                
                // Try to deserialize with better error handling
                try
                {
                    var result = JsonSerializer.Deserialize<ApiResponseDto<List<ProjectDto>>>(content, _jsonOptions);
                    _logger.LogDebug("Deserialized result: Success={Success}, Data count={Count}", 
                        result?.Success, result?.Data?.Count);
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization failed");
                    _logger.LogDebug("Content that failed to deserialize: {Content}", content);
                    return new ApiResponseDto<List<ProjectDto>> 
                    { 
                        Success = false, 
                        Message = $"Invalid JSON response: {ex.Message}",
                        Data = new List<ProjectDto>()
                    };
                }
            }
            else
            {
                _logger.LogError("Get projects failed. Status: {StatusCode}", response.StatusCode);
                return new ApiResponseDto<List<ProjectDto>> 
                { 
                    Success = false, 
                    Message = $"HTTP {response.StatusCode}",
                    Data = new List<ProjectDto>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetProjectsAsync");
            return new ApiResponseDto<List<ProjectDto>> 
            { 
                Success = false, 
                Message = ex.Message,
                Data = new List<ProjectDto>()
            };
        }
    }

    public async Task<ApiResponseDto<ProjectDto>?> GetProjectAsync(Guid projectId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<ProjectDto>>($"api/projects/{projectId}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<ProjectDto>?> CreateProjectAsync(CreateProjectDto createProject)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/projects", createProject);
            var content = await response.Content.ReadAsStringAsync();
            
            // Log response details for debugging
            _logger.LogDebug("Create project response. Status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                // Check if content is empty or not JSON
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Response content is empty");
                    return new ApiResponseDto<ProjectDto> 
                    { 
                        Success = false, 
                        Message = "Empty response from server"
                    };
                }
                
                // Try to deserialize with better error handling
                try
                {
                    var result = JsonSerializer.Deserialize<ApiResponseDto<ProjectDto>>(content, _jsonOptions);
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization failed");
                    _logger.LogDebug("Content that failed to deserialize: {Content}", content);
                    return new ApiResponseDto<ProjectDto> 
                    { 
                        Success = false, 
                        Message = $"Invalid JSON response: {ex.Message}"
                    };
                }
            }
            else
            {
                // Log the error for debugging
                _logger.LogError("Create project failed. Status: {StatusCode}", response.StatusCode);
                
                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Your session has expired. Please refresh the page and log in again.",
                    System.Net.HttpStatusCode.Forbidden => "You don't have permission to create projects.",
                    System.Net.HttpStatusCode.BadRequest => "Invalid project data. Please check your input.",
                    _ => $"Failed to create project: {response.StatusCode}"
                };
                
                return new ApiResponseDto<ProjectDto> 
                { 
                    Success = false, 
                    Message = errorMessage
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateProjectAsync");
            return new ApiResponseDto<ProjectDto> 
            { 
                Success = false, 
                Message = $"Exception: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponseDto<ProjectDto>?> UpdateProjectAsync(Guid projectId, UpdateProjectDto updateProject)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/projects/{projectId}", updateProject);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<ProjectDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Tickets API
    public async Task<ApiResponseDto<List<TicketDto>>?> GetProjectTicketsAsync(Guid projectId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponseDto<PagedResultDto<TicketDto>>>($"api/tickets/project/{projectId}", _jsonOptions);
            if (response?.Success == true && response.Data?.Items != null)
            {
                return new ApiResponseDto<List<TicketDto>>
                {
                    Success = true,
                    Data = response.Data.Items,
                    Message = response.Message
                };
            }
            return response != null ? new ApiResponseDto<List<TicketDto>> 
            { 
                Success = response.Success, 
                Message = response.Message, 
                Data = new List<TicketDto>() 
            } : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<TicketDetailDto>?> GetTicketAsync(Guid ticketId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<TicketDetailDto>>($"api/tickets/{ticketId}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<TicketDto>?> CreateTicketAsync(Guid projectId, CreateTicketDto createTicket)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/tickets/project/{projectId}", createTicket);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("CreateTicket Response - Status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(content, _jsonOptions);
            }
            else
            {
                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(content, _jsonOptions);
                    return errorResponse;
                }
                catch
                {
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            return null;
        }
    }

    public async Task<ApiResponseDto<TicketDto>?> UpdateTicketAsync(Guid ticketId, UpdateTicketDto updateTicket)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tickets/{ticketId}", updateTicket);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<TicketDto>?> UpdateTicketStatusAsync(Guid ticketId, UpdateTicketStatusDto statusUpdate)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tickets/{ticketId}/status", statusUpdate);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<TicketDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<TicketAssignmentDto>?> AssignTicketAsync(Guid ticketId, AssignTicketDto assignTicket)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/tickets/{ticketId}/assign", assignTicket);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<ApiResponseDto<TicketAssignmentDto>>(content, _jsonOptions);
            }
            else
            {
                // Try to deserialize error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponseDto<TicketAssignmentDto>>(content, _jsonOptions);
                    return errorResponse;
                }
                catch
                {
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            return null;
        }
    }

    // Comments API
    public async Task<ApiResponseDto<CommentDto>?> AddCommentAsync(Guid ticketId, CreateCommentDto createComment)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/comments/ticket/{ticketId}", createComment);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<CommentDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Get ticket comments
    public async Task<ApiResponseDto<List<CommentDto>>?> GetTicketCommentsAsync(Guid ticketId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<CommentDto>>>($"api/tickets/{ticketId}/comments", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Notifications API
    public async Task<ApiResponseDto<List<NotificationDto>>?> GetNotificationsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<NotificationDto>>>("api/notifications", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> MarkNotificationAsReadAsync(Guid notificationId)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"api/notifications/{notificationId}/read", null);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<bool>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> DeleteNotificationAsync(Guid notificationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/notifications/{notificationId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<bool>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Project Members API
    public async Task<ApiResponseDto<List<ProjectMemberDto>>?> GetProjectMembersAsync(Guid projectId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<ProjectMemberDto>>>($"api/projects/{projectId}/members", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<ProjectMemberDto>?> AddProjectMemberAsync(Guid projectId, AddProjectMemberDto addMember)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/projects/{projectId}/members", addMember);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<ProjectMemberDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Search API
    public async Task<ApiResponseDto<PagedResultDto<TicketDto>>?> SearchTicketsAsync(Guid projectId, string? keyword = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(keyword))
                queryParams.Add($"keyword={Uri.EscapeDataString(keyword)}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var query = string.Join("&", queryParams);
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<PagedResultDto<TicketDto>>>($"api/projects/{projectId}/tickets/search?{query}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Delete API methods
    public async Task<ApiResponseDto<string>?> DeleteProjectAsync(Guid projectId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/projects/{projectId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<string>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<string>?> DeleteTicketAsync(Guid ticketId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/tickets/{ticketId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<string>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Organizations API
    public async Task<ApiResponseDto<List<OrganizationDto>>?> GetOrganizationsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<OrganizationDto>>>("api/organizations", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<List<UserOrganizationDto>>?> GetOrganizationsWithRolesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<UserOrganizationDto>>>("api/organizations/with-roles", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<OrganizationDto>?> GetOrganizationAsync(Guid organizationId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<OrganizationDto>>($"api/organizations/{organizationId}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<OrganizationDto>?> CreateOrganizationAsync(CreateOrganizationDto createOrganization)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/organizations", createOrganization);
            var content = await response.Content.ReadAsStringAsync();
            
            // Log the request and response for debugging
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<ApiResponseDto<OrganizationDto>>(content, _jsonOptions);
            }
            else
            {
                // Try to parse error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponseDto<OrganizationDto>>(content, _jsonOptions);
                    
                    // Log error details
                    if (errorResponse != null)
                    {
                        if (errorResponse.Errors?.Any() == true)
                        {
                        }
                    }
                    
                    return errorResponse;
                }
                catch (JsonException jsonEx)
                {
                    
                    // If parsing fails, return a generic error
                    return new ApiResponseDto<OrganizationDto>
                    {
                        Success = false,
                        Message = $"Request failed with status {response.StatusCode}: {response.ReasonPhrase}",
                        Errors = new List<string> { content }
                    };
                }
            }
        }
        catch (HttpRequestException ex)
        {
            return new ApiResponseDto<OrganizationDto>
            {
                Success = false,
                Message = "Network error occurred. Please check your connection and try again.",
                Errors = new List<string> { ex.Message }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponseDto<OrganizationDto>
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ApiResponseDto<OrganizationDto>?> UpdateOrganizationAsync(Guid organizationId, UpdateOrganizationDto updateOrganization)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/organizations/{organizationId}", updateOrganization);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<OrganizationDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<string>?> DeleteOrganizationAsync(Guid organizationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/organizations/{organizationId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<string>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Organization Members API
    public async Task<ApiResponseDto<List<OrganizationMemberDto>>?> GetOrganizationMembersAsync(Guid organizationId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<OrganizationMemberDto>>>($"api/organizations/{organizationId}/members", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<OrganizationMemberDto>?> AddOrganizationMemberAsync(Guid organizationId, AddOrganizationMemberDto addMember)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/organizations/{organizationId}/members", addMember);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<OrganizationMemberDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<OrganizationMemberDto>?> UpdateOrganizationMemberRoleAsync(Guid organizationId, string userId, UpdateOrganizationMemberDto updateRole)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/organizations/{organizationId}/members/{userId}/role", updateRole);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<OrganizationMemberDto>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<string>?> RemoveOrganizationMemberAsync(Guid organizationId, string userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/organizations/{organizationId}/members/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponseDto<string>>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<List<ProjectDto>>?> GetOrganizationProjectsAsync(Guid organizationId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<ProjectDto>>>($"api/organizations/{organizationId}/projects", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Users API
    public async Task<ApiResponseDto<UserDetailDto>?> GetCurrentUserDetailsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<UserDetailDto>>("api/users/me/details", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<List<UserDto>>?> SearchUsersAsync(string searchTerm)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<UserDto>>>($"api/users/search?q={Uri.EscapeDataString(searchTerm)}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<UserDto>?> GetUserAsync(string userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<UserDto>>($"api/users/{userId}", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 新しいユーザー管理機能のメソッド

    public async Task<ApiResponseDto<CreateUserResult>?> CreateUserAsync(CreateUserDto createUserDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/users", createUserDto, _jsonOptions);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<CreateUserResult>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<InviteUserResult>?> InviteUserAsync(InviteUserDto inviteDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/users/invite", inviteDto, _jsonOptions);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<InviteUserResult>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<CreateUserResult>?> CreateUserForOrganizationAsync(CreateUserForOrganizationDto createUserDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/users/create-for-organization", createUserDto, _jsonOptions);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<CreateUserResult>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<ResetPasswordResult>?> ResetPasswordAsync(string userId, bool temporary = true)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/users/{userId}/reset-password?temporary={temporary}", null);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<ResetPasswordResult>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> UpdateUserRoleAsync(string userId, UpdateUserRoleDto updateDto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}/role", updateDto, _jsonOptions);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<bool>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> GrantSystemAdminAsync(string userId, GrantSystemAdminDto grantDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/grant-system-admin", grantDto, _jsonOptions);
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<bool>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> RevokeSystemAdminAsync(string userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/users/{userId}/revoke-system-admin");
            return await response.Content.ReadFromJsonAsync<ApiResponseDto<bool>>(_jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<List<SystemAdminDto>>?> GetSystemAdminsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/users/system-admins");
            var content = await response.Content.ReadAsStringAsync();
            
            
            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Response content is empty");
                    return new ApiResponseDto<List<SystemAdminDto>> 
                    { 
                        Success = false, 
                        Message = "Empty response from server",
                        Data = new List<SystemAdminDto>()
                    };
                }
                
                try
                {
                    var result = JsonSerializer.Deserialize<ApiResponseDto<List<SystemAdminDto>>>(content, _jsonOptions);
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON deserialization failed");
                    _logger.LogDebug("Content that failed to deserialize: {Content}", content);
                    return new ApiResponseDto<List<SystemAdminDto>> 
                    { 
                        Success = false, 
                        Message = $"Invalid JSON response: {ex.Message}",
                        Data = new List<SystemAdminDto>()
                    };
                }
            }
            else
            {
                return new ApiResponseDto<List<SystemAdminDto>> 
                { 
                    Success = false, 
                    Message = $"HTTP {response.StatusCode}",
                    Data = new List<SystemAdminDto>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            return new ApiResponseDto<List<SystemAdminDto>> 
            { 
                Success = false, 
                Message = $"Exception: {ex.Message}",
                Data = new List<SystemAdminDto>()
            };
        }
    }

    public async Task<ApiResponseDto<bool>?> IsSystemAdminAsync(string userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<bool>>($"api/users/{userId}/is-system-admin", _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ApiResponseDto<bool>?> IsOrganizationAdminAsync(string userId, Guid? organizationId = null)
    {
        try
        {
            var url = $"api/users/{userId}/is-organization-admin";
            if (organizationId.HasValue)
            {
                url += $"?organizationId={organizationId}";
            }
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<bool>>(url, _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}