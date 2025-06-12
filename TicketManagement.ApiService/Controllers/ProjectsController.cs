using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                     User.FindFirst("sub")?.Value;
        
        _logger.LogDebug("GetCurrentUserId called. UserId: {UserId}, Claims: {Claims}", 
            userId, 
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        
        return userId;
    }

    /// <summary>
    /// ユーザーが参加しているプロジェクト一覧を取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<ProjectDto>>>> GetProjects()
    {
        try
        {
            var userId = GetCurrentUserId();
            var projects = await _projectService.GetProjectsByUserAsync(userId);
            
            var projectDtos = projects.Select(p => new ProjectDto
            {
                Id = p.Id,
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

            _logger.LogInformation("Returning {ProjectCount} projects for user {UserId}", projectDtos.Count, userId);
            var result = ApiResponseDto<List<ProjectDto>>.SuccessResult(projectDtos);
            
            // Debug: Log the serialized result
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result);
                _logger.LogDebug("GetProjects serialized response: {Json}", json);
            }
            catch (Exception serEx)
            {
                _logger.LogError(serEx, "Failed to serialize GetProjects response for debugging");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects for user");
            return StatusCode(500, ApiResponseDto<List<ProjectDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクト詳細を取得
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<ProjectDto>>> GetProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // ユーザーがプロジェクトのメンバーかチェック
            if (!await _projectService.CanUserAccessProjectAsync(id, userId))
            {
                return Forbid();
            }

            var project = await _projectService.GetProjectAsync(id);
            if (project == null)
            {
                return NotFound(ApiResponseDto<ProjectDto>.ErrorResult("Project not found"));
            }

            var projectDto = new ProjectDto
            {
                Id = project.Id,
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
                TicketCount = project.Tickets?.Count ?? 0
            };

            return ApiResponseDto<ProjectDto>.SuccessResult(projectDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 新規プロジェクトを作成
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<ProjectDto>>> CreateProject([FromBody] CreateProjectDto dto)
    {
        _logger.LogInformation("CreateProject called with Name: {Name}, Description: {Description}", dto?.Name, dto?.Description);
        
        try
        {
            if (dto == null)
            {
                _logger.LogWarning("CreateProject called with null DTO");
                return BadRequest(ApiResponseDto<ProjectDto>.ErrorResult("Project data is required"));
            }
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("CreateProject validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseDto<ProjectDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            _logger.LogInformation("Creating project for user: {UserId}", userId);
            var project = await _projectService.CreateProjectAsync(dto.Name, dto.Description, userId);

            var projectDto = new ProjectDto
            {
                Id = project.Id,
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

            _logger.LogInformation("Project created successfully: {ProjectId}", project.Id);
            var result = ApiResponseDto<ProjectDto>.SuccessResult(projectDto, "Project created successfully");
            
            // Debug: Log the serialized result
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result);
                _logger.LogDebug("Serialized response: {Json}", json);
            }
            catch (Exception serEx)
            {
                _logger.LogError(serEx, "Failed to serialize response for debugging");
            }
            
            return Created($"api/projects/{project.Id}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project: {Message}", ex.Message);
            return StatusCode(500, ApiResponseDto<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトを更新
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ProjectManager")]
    public async Task<ActionResult<ApiResponseDto<ProjectDto>>> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
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
            
            // ユーザーがプロジェクトを管理できるかチェック
            if (!await _projectService.CanUserManageProjectAsync(id, userId))
            {
                return Forbid();
            }

            var project = await _projectService.UpdateProjectAsync(id, dto.Name, dto.Description, userId);

            var projectDto = new ProjectDto
            {
                Id = project.Id,
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
                TicketCount = project.Tickets?.Count ?? 0
            };

            return ApiResponseDto<ProjectDto>.SuccessResult(projectDto, "Project updated successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<ProjectDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトのメンバー一覧を取得
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<ApiResponseDto<List<ProjectMemberDto>>>> GetProjectMembers(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.CanUserAccessProjectAsync(id, userId))
            {
                return Forbid();
            }

            var members = await _projectService.GetProjectMembersAsync(id);
            var memberDtos = members.Select(m => new ProjectMemberDto
            {
                Id = m.Id,
                ProjectId = m.ProjectId,
                UserId = m.UserId,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList();

            return ApiResponseDto<List<ProjectMemberDto>>.SuccessResult(memberDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project members for {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<List<ProjectMemberDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトにメンバーを追加
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = "ProjectManager")]
    public async Task<ActionResult<ApiResponseDto<ProjectMemberDto>>> AddProjectMember(Guid id, [FromBody] AddProjectMemberDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<ProjectMemberDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            
            if (!await _projectService.CanUserManageProjectAsync(id, userId))
            {
                return Forbid();
            }

            var member = await _projectService.AddMemberAsync(id, dto.UserId, dto.Role, userId);

            var memberDto = new ProjectMemberDto
            {
                Id = member.Id,
                ProjectId = member.ProjectId,
                UserId = member.UserId,
                Role = member.Role,
                JoinedAt = member.JoinedAt
            };

            return ApiResponseDto<ProjectMemberDto>.SuccessResult(memberDto, "Member added successfully");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<ProjectMemberDto>.ErrorResult(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<ProjectMemberDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to project {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<ProjectMemberDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトメンバーの権限を更新
    /// </summary>
    [HttpPut("{id:guid}/members/{userId}")]
    [Authorize(Policy = "ProjectManager")]
    public async Task<ActionResult<ApiResponseDto<ProjectMemberDto>>> UpdateProjectMemberRole(
        Guid id, string userId, [FromBody] UpdateProjectMemberDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<ProjectMemberDto>.ErrorResult(errors));
            }

            var currentUserId = GetCurrentUserId();
            
            if (!await _projectService.CanUserManageProjectAsync(id, currentUserId))
            {
                return Forbid();
            }

            var member = await _projectService.UpdateMemberRoleAsync(id, userId, dto.Role, currentUserId);

            var memberDto = new ProjectMemberDto
            {
                Id = member.Id,
                ProjectId = member.ProjectId,
                UserId = member.UserId,
                Role = member.Role,
                JoinedAt = member.JoinedAt
            };

            return ApiResponseDto<ProjectMemberDto>.SuccessResult(memberDto, "Member role updated successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<ProjectMemberDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role in project {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<ProjectMemberDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトからメンバーを削除
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    [Authorize(Policy = "ProjectManager")]
    public async Task<ActionResult<ApiResponseDto<string>>> RemoveProjectMember(Guid id, string userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            if (!await _projectService.CanUserManageProjectAsync(id, currentUserId))
            {
                return Forbid();
            }

            await _projectService.RemoveMemberAsync(id, userId, currentUserId);

            return ApiResponseDto<string>.SuccessResult("success", "Member removed successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from project {ProjectId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }
}