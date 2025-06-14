using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.Services;
using TicketManagement.Contracts.DTOs;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserManagementService userManagementService,
        ILogger<UsersController> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    /// <summary>
    /// 現在のユーザー情報を取得
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
    {
        try
        {
            var user = await _userManagementService.GetCurrentUserAsync();
            if (user == null)
            {
                return NotFound(ApiResponseDto<UserDto>.ErrorResult("User not found"));
            }

            return ApiResponseDto<UserDto>.SuccessResult(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, ApiResponseDto<UserDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 現在のユーザーの詳細情報を取得（組織・プロジェクト情報含む）
    /// </summary>
    [HttpGet("me/details")]
    public async Task<ActionResult<ApiResponseDto<UserDetailDto>>> GetCurrentUserDetails()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Getting user details for userId: {UserId}", userId);
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponseDto<UserDetailDto>.ErrorResult("User not authenticated"));
            }

            var userDetail = await _userManagementService.GetUserDetailAsync(userId);
            _logger.LogInformation("User detail found: {Found}, Organizations count: {OrgCount}", 
                userDetail != null, userDetail?.Organizations?.Count ?? 0);
                
            if (userDetail == null)
            {
                return NotFound(ApiResponseDto<UserDetailDto>.ErrorResult("User details not found"));
            }

            return ApiResponseDto<UserDetailDto>.SuccessResult(userDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user details");
            return StatusCode(500, ApiResponseDto<UserDetailDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーを検索
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> SearchUsers(
        [FromQuery] string? q,
        [FromQuery] int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(ApiResponseDto<List<UserDto>>.ErrorResult("Search term is required"));
            }

            if (maxResults > 100)
            {
                maxResults = 100; // Limit to prevent abuse
            }

            var users = await _userManagementService.SearchUsersAsync(q, maxResults);
            return ApiResponseDto<List<UserDto>>.SuccessResult(users.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with term: {SearchTerm}", q);
            return StatusCode(500, ApiResponseDto<List<UserDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 特定のユーザー情報を取得
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetUser(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponseDto<UserDto>.ErrorResult("User ID is required"));
            }

            var user = await _userManagementService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(ApiResponseDto<UserDto>.ErrorResult("User not found"));
            }

            return ApiResponseDto<UserDto>.SuccessResult(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            return StatusCode(500, ApiResponseDto<UserDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 特定のユーザーの詳細情報を取得
    /// </summary>
    [HttpGet("{userId}/details")]
    public async Task<ActionResult<ApiResponseDto<UserDetailDto>>> GetUserDetails(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponseDto<UserDetailDto>.ErrorResult("User ID is required"));
            }

            var userDetail = await _userManagementService.GetUserDetailAsync(userId);
            if (userDetail == null)
            {
                return NotFound(ApiResponseDto<UserDetailDto>.ErrorResult("User details not found"));
            }

            return ApiResponseDto<UserDetailDto>.SuccessResult(userDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user details for {UserId}", userId);
            return StatusCode(500, ApiResponseDto<UserDetailDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーの組織一覧を取得
    /// </summary>
    [HttpGet("{userId}/organizations")]
    public async Task<ActionResult<ApiResponseDto<List<UserOrganizationDto>>>> GetUserOrganizations(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponseDto<List<UserOrganizationDto>>.ErrorResult("User ID is required"));
            }

            // Check if current user can access this information
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId)
            {
                // Only allow access to own organizations for now
                // In future, we might allow organization admins to see member organizations
                return Forbid();
            }

            var organizations = await _userManagementService.GetUserOrganizationsAsync(userId);
            return ApiResponseDto<List<UserOrganizationDto>>.SuccessResult(organizations.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations for user {UserId}", userId);
            return StatusCode(500, ApiResponseDto<List<UserOrganizationDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 複数のユーザー情報を一括取得
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<ApiResponseDto<Dictionary<string, UserDto>>>> GetUsersBatch(
        [FromBody] List<string> userIds)
    {
        try
        {
            if (userIds == null || userIds.Count == 0)
            {
                return BadRequest(ApiResponseDto<Dictionary<string, UserDto>>.ErrorResult("User IDs are required"));
            }

            if (userIds.Count > 100)
            {
                return BadRequest(ApiResponseDto<Dictionary<string, UserDto>>.ErrorResult("Maximum 100 user IDs allowed"));
            }

            var users = await _userManagementService.GetUsersByIdsAsync(userIds);
            return ApiResponseDto<Dictionary<string, UserDto>>.SuccessResult(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users batch");
            return StatusCode(500, ApiResponseDto<Dictionary<string, UserDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーの有効性を確認
    /// </summary>
    [HttpGet("{userId}/status")]
    public async Task<ActionResult<ApiResponseDto<bool>>> IsUserActive(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponseDto<bool>.ErrorResult("User ID is required"));
            }

            var isActive = await _userManagementService.IsUserActiveAsync(userId);
            return ApiResponseDto<bool>.SuccessResult(isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user status for {UserId}", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? User.FindFirst("sub")?.Value;
    }
}