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

            return Ok(ApiResponseDto<UserDto>.SuccessResult(user));
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

            return Ok(ApiResponseDto<UserDetailDto>.SuccessResult(userDetail));
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
            return Ok(ApiResponseDto<List<UserDto>>.SuccessResult(users.ToList()));
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

            return Ok(ApiResponseDto<UserDto>.SuccessResult(user));
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

            return Ok(ApiResponseDto<UserDetailDto>.SuccessResult(userDetail));
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
            return Ok(ApiResponseDto<List<UserOrganizationDto>>.SuccessResult(organizations.ToList()));
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
            return Ok(ApiResponseDto<Dictionary<string, UserDto>>.SuccessResult(users));
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
            return Ok(ApiResponseDto<bool>.SuccessResult(isActive));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user status for {UserId}", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    // 新しいユーザー管理機能のエンドポイント

    /// <summary>
    /// 新規ユーザーを作成
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<CreateUserResult>>> CreateUser(
        [FromBody] CreateUserDto createUserDto)
    {
        try
        {
            var result = await _userManagementService.CreateUserAsync(createUserDto);
            return Ok(ApiResponseDto<CreateUserResult>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", createUserDto.Username);
            return StatusCode(500, ApiResponseDto<CreateUserResult>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーを組織に招待
    /// </summary>
    [HttpPost("invite")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<InviteUserResult>>> InviteUser(
        [FromBody] InviteUserDto inviteDto)
    {
        try
        {
            // 現在のユーザーが指定された組織の管理者であることを確認
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(ApiResponseDto<InviteUserResult>.ErrorResult("Unable to identify current user"));
            }

            // システム管理者または指定された組織の管理者かどうかを確認
            var isSystemAdmin = await _userManagementService.IsSystemAdminAsync(currentUserId);
            var isOrgAdmin = await _userManagementService.IsOrganizationAdminAsync(currentUserId, inviteDto.OrganizationId);
            
            if (!isSystemAdmin && !isOrgAdmin)
            {
                return Forbid("You don't have permission to invite users to this organization");
            }

            var result = await _userManagementService.InviteUserToOrganizationAsync(inviteDto);
            return Ok(ApiResponseDto<InviteUserResult>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user {Email}", inviteDto.Email);
            return StatusCode(500, ApiResponseDto<InviteUserResult>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織管理者が組織内でユーザーを作成
    /// </summary>
    [HttpPost("create-for-organization")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<CreateUserResult>>> CreateUserForOrganization(
        [FromBody] CreateUserForOrganizationDto createUserDto)
    {
        try
        {
            // 現在のユーザーが指定された組織の管理者であることを確認
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(ApiResponseDto<CreateUserResult>.ErrorResult("Unable to identify current user"));
            }

            // システム管理者または指定された組織の管理者かどうかを確認
            var isSystemAdmin = await _userManagementService.IsSystemAdminAsync(currentUserId);
            var isOrgAdmin = await _userManagementService.IsOrganizationAdminAsync(currentUserId, createUserDto.OrganizationId);
            
            if (!isSystemAdmin && !isOrgAdmin)
            {
                return Forbid("You don't have permission to create users for this organization");
            }

            // CreateUserDtoに変換
            var createDto = new CreateUserDto
            {
                Username = createUserDto.Username,
                Email = createUserDto.Email,
                FirstName = createUserDto.FirstName,
                LastName = createUserDto.LastName,
                TemporaryPassword = createUserDto.Password,
                RequirePasswordChange = createUserDto.RequirePasswordChange,
                IsActive = true,
                RoleAssignments = new List<UserRoleAssignmentDto>
                {
                    new()
                    {
                        OrganizationId = createUserDto.OrganizationId,
                        Role = createUserDto.Role
                    }
                }
            };

            var result = await _userManagementService.CreateUserAsync(createDto);
            return Ok(ApiResponseDto<CreateUserResult>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user for organization {OrganizationId}", createUserDto.OrganizationId);
            return StatusCode(500, ApiResponseDto<CreateUserResult>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーのパスワードをリセット（システム管理者のみ）
    /// </summary>
    [HttpPost("{userId}/reset-password")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<ResetPasswordResult>>> ResetPassword(
        string userId, [FromQuery] bool temporary = true)
    {
        try
        {
            var result = await _userManagementService.ResetUserPasswordAsync(userId, temporary);
            return Ok(ApiResponseDto<ResetPasswordResult>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return StatusCode(500, ApiResponseDto<ResetPasswordResult>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーの権限を変更
    /// </summary>
    [HttpPut("{userId}/role")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<bool>>> UpdateUserRole(
        string userId, [FromBody] UpdateUserRoleDto updateDto)
    {
        try
        {
            // 現在のユーザーが指定された組織の管理者であることを確認
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(ApiResponseDto<bool>.ErrorResult("Unable to identify current user"));
            }

            // システム管理者または指定された組織の管理者かどうかを確認
            var isSystemAdmin = await _userManagementService.IsSystemAdminAsync(currentUserId);
            var isOrgAdmin = await _userManagementService.IsOrganizationAdminAsync(currentUserId, updateDto.OrganizationId);
            
            if (!isSystemAdmin && !isOrgAdmin)
            {
                return Forbid("You don't have permission to update user roles in this organization");
            }

            var result = await _userManagementService.UpdateUserRoleAsync(
                updateDto.OrganizationId, userId, updateDto.NewRole);
            return Ok(ApiResponseDto<bool>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role for {UserId}", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// システム管理者権限を付与
    /// </summary>
    [HttpPost("{userId}/grant-system-admin")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<bool>>> GrantSystemAdmin(
        string userId, [FromBody] GrantSystemAdminDto grantDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(ApiResponseDto<bool>.ErrorResult("Unable to identify current user"));
            }

            grantDto.UserId = userId;
            var result = await _userManagementService.GrantSystemAdminAsync(grantDto, currentUserId);
            return Ok(ApiResponseDto<bool>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting system admin to user {UserId}", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// システム管理者権限を削除
    /// </summary>
    [HttpDelete("{userId}/revoke-system-admin")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<bool>>> RevokeSystemAdmin(string userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(ApiResponseDto<bool>.ErrorResult("Unable to identify current user"));
            }

            var result = await _userManagementService.RevokeSystemAdminAsync(userId, currentUserId);
            return Ok(ApiResponseDto<bool>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking system admin from user {UserId}", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// システム管理者一覧を取得
    /// </summary>
    [HttpGet("system-admins")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<List<SystemAdminDto>>>> GetSystemAdmins()
    {
        try
        {
            var admins = await _userManagementService.GetSystemAdminsAsync();
            return Ok(ApiResponseDto<List<SystemAdminDto>>.SuccessResult(admins.ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system admins");
            return StatusCode(500, ApiResponseDto<List<SystemAdminDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーがシステム管理者かどうかを確認
    /// </summary>
    [HttpGet("{userId}/is-system-admin")]
    public async Task<ActionResult<ApiResponseDto<bool>>> IsSystemAdmin(string userId)
    {
        try
        {
            var result = await _userManagementService.IsSystemAdminAsync(userId);
            return Ok(ApiResponseDto<bool>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is system admin", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーが組織管理者かどうかを確認
    /// </summary>
    [HttpGet("{userId}/is-organization-admin")]
    public async Task<ActionResult<ApiResponseDto<bool>>> IsOrganizationAdmin(
        string userId, [FromQuery] Guid? organizationId = null)
    {
        try
        {
            var result = await _userManagementService.IsOrganizationAdminAsync(userId, organizationId);
            return Ok(ApiResponseDto<bool>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is organization admin", userId);
            return StatusCode(500, ApiResponseDto<bool>.ErrorResult("Internal server error"));
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? User.FindFirst("sub")?.Value;
    }
}