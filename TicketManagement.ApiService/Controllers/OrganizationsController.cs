using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.ApiService.Common;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IProjectService _projectService;
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationService organizationService, 
        IProjectService projectService,
        IUserManagementService userManagementService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _projectService = projectService;
        _userManagementService = userManagementService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                     User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        
        return userId;
    }

    /// <summary>
    /// ユーザーIDを表示名に解決
    /// </summary>
    private async Task<string?> ResolveUserDisplayNameAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        try
        {
            var user = await _userManagementService.GetUserByIdAsync(userId);
            return user?.DisplayName ?? user?.Username ?? $"User-{userId[..Math.Min(8, userId.Length)]}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve display name for user {UserId}", userId);
            return $"User-{userId[..Math.Min(8, userId.Length)]}";
        }
    }

    /// <summary>
    /// 複数のユーザーIDを一括で表示名に解決
    /// </summary>
    private async Task<Dictionary<string, string?>> ResolveUserDisplayNamesAsync(IEnumerable<string> userIds)
    {
        var uniqueUserIds = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (!uniqueUserIds.Any())
            return new Dictionary<string, string?>();

        try
        {
            var users = await _userManagementService.GetUsersByIdsAsync(uniqueUserIds);
            return users.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.DisplayName ?? kvp.Value?.Username ?? $"User-{kvp.Key[..Math.Min(8, kvp.Key.Length)]}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve display names for users");
            return uniqueUserIds.ToDictionary(id => id, id => $"User-{id[..Math.Min(8, id.Length)]}");
        }
    }

    /// <summary>
    /// ユーザーが所属する組織の一覧を取得（ロール情報含む）
    /// </summary>
    [HttpGet("with-roles")]
    public async Task<ActionResult<ApiResponseDto<List<UserOrganizationDto>>>> GetUserOrganizationsWithRoles()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Getting organizations with roles for user: {UserId}", userId);
            
            var organizationMembers = await _organizationService.GetOrganizationMembersForUserAsync(userId);
            
            var userOrgDtos = new List<UserOrganizationDto>();
            foreach (var member in organizationMembers)
            {
                if (member.Organization != null && member.Organization.IsActive && member.IsActive)
                {
                    userOrgDtos.Add(new UserOrganizationDto
                    {
                        OrganizationId = member.OrganizationId,
                        OrganizationName = member.Organization.Name,
                        OrganizationDisplayName = member.Organization.DisplayName,
                        Role = member.Role,
                        JoinedAt = member.JoinedAt,
                        IsActive = member.IsActive
                    });
                }
            }
            
            _logger.LogInformation("Found {Count} organization memberships with roles for user {UserId}", userOrgDtos.Count, userId);
            return ApiResponseDto<List<UserOrganizationDto>>.SuccessResult(userOrgDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user organizations with roles");
            return StatusCode(500, ApiResponseDto<List<UserOrganizationDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーが所属する組織の一覧を取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<OrganizationDto>>>> GetUserOrganizations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var organizations = await _organizationService.GetUserOrganizationsAsync(userId);
            
            // Collect all unique user IDs for batch resolution
            var allUserIds = organizations
                .SelectMany(org => new[] { org.CreatedBy, org.UpdatedBy })
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            
            var userDisplayNames = await ResolveUserDisplayNamesAsync(allUserIds);
            
            var orgDtos = new List<OrganizationDto>();
            foreach (var org in organizations)
            {
                var (currentProjects, maxProjects) = await _organizationService.GetProjectLimitsAsync(org.Id);
                var (currentMembers, maxMembers) = await _organizationService.GetMemberLimitsAsync(org.Id);
                
                orgDtos.Add(new OrganizationDto
                {
                    Id = org.Id,
                    Name = org.Name,
                    DisplayName = org.DisplayName,
                    Description = org.Description,
                    CreatedAt = org.CreatedAt,
                    CreatedBy = org.CreatedBy,
                    CreatedByName = !string.IsNullOrEmpty(org.CreatedBy) ? userDisplayNames.GetValueOrDefault(org.CreatedBy) : null,
                    UpdatedAt = org.UpdatedAt,
                    UpdatedBy = org.UpdatedBy,
                    UpdatedByName = !string.IsNullOrEmpty(org.UpdatedBy) ? userDisplayNames.GetValueOrDefault(org.UpdatedBy) : null,
                    IsActive = org.IsActive,
                    MaxProjects = maxProjects,
                    MaxMembers = maxMembers,
                    CurrentProjects = currentProjects,
                    CurrentMembers = currentMembers
                });
            }
            
            return Ok(ApiResponseDto<List<OrganizationDto>>.SuccessResult(orgDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user organizations");
            return StatusCode(500, ApiResponseDto<List<OrganizationDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織の詳細を取得
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<OrganizationDto>>> GetOrganization(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _organizationService.CanUserAccessOrganizationAsync(id, userId))
            {
                return Forbid();
            }

            var organization = await _organizationService.GetOrganizationWithDetailsAsync(id);
            if (organization == null)
            {
                return NotFound(ApiResponseDto<OrganizationDto>.ErrorResult("Organization not found"));
            }

            var (currentProjects, maxProjects) = await _organizationService.GetProjectLimitsAsync(id);
            var (currentMembers, maxMembers) = await _organizationService.GetMemberLimitsAsync(id);
            
            // Resolve user display names for creator and updater
            var userIds = new List<string>();
            if (!string.IsNullOrEmpty(organization.CreatedBy)) userIds.Add(organization.CreatedBy);
            if (!string.IsNullOrEmpty(organization.UpdatedBy)) userIds.Add(organization.UpdatedBy);
            
            var userDisplayNames = await ResolveUserDisplayNamesAsync(userIds);
            
            var orgDto = new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                DisplayName = organization.DisplayName,
                Description = organization.Description,
                CreatedAt = organization.CreatedAt,
                CreatedBy = organization.CreatedBy,
                CreatedByName = !string.IsNullOrEmpty(organization.CreatedBy) ? userDisplayNames.GetValueOrDefault(organization.CreatedBy) : null,
                UpdatedAt = organization.UpdatedAt,
                UpdatedBy = organization.UpdatedBy,
                UpdatedByName = !string.IsNullOrEmpty(organization.UpdatedBy) ? userDisplayNames.GetValueOrDefault(organization.UpdatedBy) : null,
                IsActive = organization.IsActive,
                MaxProjects = maxProjects,
                MaxMembers = maxMembers,
                CurrentProjects = currentProjects,
                CurrentMembers = currentMembers,
                Members = organization.Members.Where(m => m.IsActive).Select(m => new OrganizationMemberDto
                {
                    Id = m.Id,
                    OrganizationId = m.OrganizationId,
                    UserId = m.UserId,
                    UserName = m.UserName,
                    UserEmail = m.UserEmail,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    InvitedBy = m.InvitedBy,
                    IsActive = m.IsActive,
                    LastAccessedAt = m.LastAccessedAt
                }).ToList()
            };

            return Ok(ApiResponseDto<OrganizationDto>.SuccessResult(orgDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<OrganizationDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 新しい組織を作成
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<OrganizationDto>>> CreateOrganization([FromBody] CreateOrganizationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogDebug("Model validation failed for CreateOrganization: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseDto<OrganizationDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            _logger.LogDebug("Creating organization: Name={Name}, DisplayName={DisplayName}, UserId={UserId}", 
                dto.Name, dto.DisplayName, userId);
            
            var organization = await _organizationService.CreateOrganizationAsync(
                dto.Name, 
                dto.DisplayName, 
                dto.Description, 
                userId);

            var orgDto = new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                DisplayName = organization.DisplayName,
                Description = organization.Description,
                CreatedAt = organization.CreatedAt,
                CreatedBy = organization.CreatedBy,
                IsActive = organization.IsActive,
                MaxProjects = organization.MaxProjects,
                MaxMembers = organization.MaxMembers,
                CurrentProjects = 0,
                CurrentMembers = 1 // Creator
            };

            _logger.LogInformation("Organization '{Name}' created successfully with ID {OrganizationId}", 
                dto.Name, organization.Id);

            return CreatedAtAction(
                nameof(GetOrganization), 
                new { id = organization.Id }, 
                ApiResponseDto<OrganizationDto>.SuccessResult(orgDto, "Organization created successfully"));
        }
        catch (Exception ex)
        {
            return ErrorHandler.HandleException<OrganizationDto>(ex, _logger, "CreateOrganization");
        }
    }

    /// <summary>
    /// 組織情報を更新
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<OrganizationDto>>> UpdateOrganization(
        Guid id, 
        [FromBody] UpdateOrganizationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<OrganizationDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            var organization = await _organizationService.UpdateOrganizationAsync(
                id, 
                dto.Name, 
                dto.DisplayName, 
                dto.Description, 
                userId);

            var (currentProjects, maxProjects) = await _organizationService.GetProjectLimitsAsync(id);
            var (currentMembers, maxMembers) = await _organizationService.GetMemberLimitsAsync(id);

            var orgDto = new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                DisplayName = organization.DisplayName,
                Description = organization.Description,
                CreatedAt = organization.CreatedAt,
                CreatedBy = organization.CreatedBy,
                UpdatedAt = organization.UpdatedAt,
                UpdatedBy = organization.UpdatedBy,
                IsActive = organization.IsActive,
                MaxProjects = maxProjects,
                MaxMembers = maxMembers,
                CurrentProjects = currentProjects,
                CurrentMembers = currentMembers
            };

            return ApiResponseDto<OrganizationDto>.SuccessResult(orgDto, "Organization updated successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<OrganizationDto>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<OrganizationDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<OrganizationDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織を削除
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteOrganization(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _organizationService.DeleteOrganizationAsync(id, userId);
            
            return ApiResponseDto<string>.SuccessResult("success", "Organization deleted successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織のメンバー一覧を取得
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<ApiResponseDto<List<OrganizationMemberDto>>>> GetOrganizationMembers(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _organizationService.CanUserAccessOrganizationAsync(id, userId))
            {
                return Forbid();
            }

            // Synchronize member information with Keycloak before returning
            try
            {
                await _userManagementService.SyncOrganizationMembersAsync(id);
            }
            catch (Exception syncEx)
            {
                _logger.LogWarning(syncEx, "Failed to synchronize members for organization {OrganizationId}, continuing with existing data", id);
            }

            var members = await _organizationService.GetOrganizationMembersAsync(id);
            
            // Collect all user IDs to resolve display names
            var userIds = members.Select(m => m.UserId).Where(uid => !string.IsNullOrEmpty(uid)).Distinct().ToList();
            var userDisplayNames = await ResolveUserDisplayNamesAsync(userIds);
            
            var memberDtos = members.Select(m => new OrganizationMemberDto
            {
                Id = m.Id,
                OrganizationId = m.OrganizationId,
                UserId = m.UserId,
                UserName = !string.IsNullOrEmpty(m.UserName) ? m.UserName : userDisplayNames.GetValueOrDefault(m.UserId) ?? m.UserId,
                UserEmail = m.UserEmail,
                Role = m.Role,
                JoinedAt = m.JoinedAt,
                InvitedBy = m.InvitedBy,
                IsActive = m.IsActive,
                LastAccessedAt = m.LastAccessedAt
            }).ToList();

            return ApiResponseDto<List<OrganizationMemberDto>>.SuccessResult(memberDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization members for {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<List<OrganizationMemberDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織メンバーの情報をKeycloakと同期
    /// </summary>
    [HttpPost("{id:guid}/members/sync")]
    public async Task<ActionResult<ApiResponseDto<string>>> SyncOrganizationMembers(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _organizationService.CanUserAccessOrganizationAsync(id, userId))
            {
                return Forbid();
            }

            // Check if user can manage members in the organization
            var canManageMembers = await _organizationService.CanUserManageMembersAsync(id, userId);
            if (!canManageMembers)
            {
                return Forbid("Only organization administrators can sync member information");
            }

            await _userManagementService.SyncOrganizationMembersAsync(id);

            return ApiResponseDto<string>.SuccessResult("Member synchronization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing organization members for {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Failed to synchronize member information"));
        }
    }

    /// <summary>
    /// 組織にメンバーを追加
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<ApiResponseDto<OrganizationMemberDto>>> AddOrganizationMember(
        Guid id, 
        [FromBody] AddOrganizationMemberDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<OrganizationMemberDto>.ErrorResult(errors));
            }

            var invitedBy = GetCurrentUserId();
            var member = await _organizationService.AddMemberAsync(
                id, 
                dto.UserId, 
                dto.UserName, 
                dto.UserEmail, 
                dto.Role, 
                invitedBy);

            var memberDto = new OrganizationMemberDto
            {
                Id = member.Id,
                OrganizationId = member.OrganizationId,
                UserId = member.UserId,
                UserName = member.UserName,
                UserEmail = member.UserEmail,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                InvitedBy = member.InvitedBy,
                IsActive = member.IsActive
            };

            return Ok(ApiResponseDto<OrganizationMemberDto>.SuccessResult(memberDto, "Member added successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<OrganizationMemberDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<OrganizationMemberDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織メンバーのロールを更新
    /// </summary>
    [HttpPut("{id:guid}/members/{userId}")]
    public async Task<ActionResult<ApiResponseDto<OrganizationMemberDto>>> UpdateMemberRole(
        Guid id, 
        string userId,
        [FromBody] UpdateOrganizationMemberDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<OrganizationMemberDto>.ErrorResult(errors));
            }

            var updatedBy = GetCurrentUserId();
            var member = await _organizationService.UpdateMemberRoleAsync(id, userId, dto.Role, updatedBy);

            var memberDto = new OrganizationMemberDto
            {
                Id = member.Id,
                OrganizationId = member.OrganizationId,
                UserId = member.UserId,
                UserName = member.UserName,
                UserEmail = member.UserEmail,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                InvitedBy = member.InvitedBy,
                IsActive = member.IsActive
            };

            return Ok(ApiResponseDto<OrganizationMemberDto>.SuccessResult(memberDto, "Member role updated successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<OrganizationMemberDto>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<OrganizationMemberDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role in organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<OrganizationMemberDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織からメンバーを削除
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<ActionResult<ApiResponseDto<string>>> RemoveOrganizationMember(Guid id, string userId)
    {
        try
        {
            var removedBy = GetCurrentUserId();
            await _organizationService.RemoveMemberAsync(id, userId, removedBy);
            
            return Ok(ApiResponseDto<string>.SuccessResult("success", "Member removed successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織内のプロジェクト一覧を取得
    /// </summary>
    [HttpGet("{id:guid}/projects")]
    public async Task<ActionResult<ApiResponseDto<List<ProjectDto>>>> GetOrganizationProjects(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _organizationService.CanUserAccessOrganizationAsync(id, userId))
            {
                return Forbid();
            }

            var projects = await _projectService.GetProjectsByOrganizationAsync(id);
            var projectDtos = projects.Select(p => new ProjectDto
            {
                Id = p.Id,
                OrganizationId = p.OrganizationId,
                Name = p.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                CreatedBy = p.CreatedBy,
                IsActive = p.IsActive,
                Members = p.Members.Select(m => new ProjectMemberDto
                {
                    Id = m.Id,
                    ProjectId = m.ProjectId,
                    UserId = m.UserId,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt
                }).ToList(),
                TicketCount = p.Tickets?.Count ?? 0
            }).ToList();

            return ApiResponseDto<List<ProjectDto>>.SuccessResult(projectDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects for organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<List<ProjectDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 組織内に新しいプロジェクトを作成
    /// </summary>
    [HttpPost("{id:guid}/projects")]
    public async Task<ActionResult<ApiResponseDto<ProjectDto>>> CreateProjectInOrganization(
        Guid id,
        [FromBody] CreateProjectDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<ProjectDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            var project = await _projectService.CreateProjectAsync(id, dto.Name, dto.Description, userId);

            var organization = await _organizationService.GetOrganizationAsync(id);
            
            var projectDto = new ProjectDto
            {
                Id = project.Id,
                OrganizationId = project.OrganizationId,
                OrganizationName = organization?.Name ?? string.Empty,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                CreatedBy = project.CreatedBy,
                IsActive = project.IsActive,
                Members = project.Members.Select(m => new ProjectMemberDto
                {
                    Id = m.Id,
                    ProjectId = m.ProjectId,
                    UserId = m.UserId,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt
                }).ToList(),
                TicketCount = 0
            };

            return Ok(ApiResponseDto<ProjectDto>.SuccessResult(projectDto, "Project created successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<ProjectDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project in organization {OrganizationId}", id);
            return StatusCode(500, ApiResponseDto<ProjectDto>.ErrorResult("Internal server error"));
        }
    }
}