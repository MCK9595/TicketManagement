using TicketManagement.Contracts.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace TicketManagement.Web.Services;

public class TicketManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TicketManagementApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            
            // Log response details for debugging
            Console.WriteLine($"Get projects response. Status: {response.StatusCode}, Content: '{content}', Content-Type: {response.Content.Headers.ContentType}");
            Console.WriteLine($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            Console.WriteLine($"Content Length: {response.Content.Headers.ContentLength}");
            
            if (response.IsSuccessStatusCode)
            {
                // Check if content is empty or not JSON
                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine("Response content is empty");
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
                    Console.WriteLine($"Deserialized result: Success={result?.Success}, Data count={result?.Data?.Count}, Message={result?.Message}");
                    return result;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization failed: {ex.Message}");
                    Console.WriteLine($"Content that failed to deserialize: {content}");
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
                Console.WriteLine($"Get projects failed. Status: {response.StatusCode}, Content: {content}");
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
            Console.WriteLine($"Exception in GetProjectsAsync: {ex.Message}");
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
            Console.WriteLine($"Create project response. Status: {response.StatusCode}, Content: '{content}', Content-Type: {response.Content.Headers.ContentType}");
            Console.WriteLine($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            Console.WriteLine($"Content Length: {response.Content.Headers.ContentLength}");
            
            if (response.IsSuccessStatusCode)
            {
                // Check if content is empty or not JSON
                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine("Response content is empty");
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
                    Console.WriteLine($"Deserialized result: Success={result?.Success}, Data={result?.Data?.Name}, Message={result?.Message}");
                    return result;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization failed: {ex.Message}");
                    Console.WriteLine($"Content that failed to deserialize: {content}");
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
                Console.WriteLine($"Create project failed. Status: {response.StatusCode}, Content: {content}");
                
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
            Console.WriteLine($"Exception in CreateProjectAsync: {ex.Message}");
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
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<TicketDto>>>($"api/projects/{projectId}/tickets", _jsonOptions);
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
            var response = await _httpClient.PostAsJsonAsync($"api/projects/{projectId}/tickets", createTicket);
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
            var response = await _httpClient.PatchAsJsonAsync($"api/tickets/{ticketId}/status", statusUpdate);
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

    // Comments API
    public async Task<ApiResponseDto<CommentDto>?> AddCommentAsync(Guid ticketId, CreateCommentDto createComment)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/tickets/{ticketId}/comments", createComment);
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
}