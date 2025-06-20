using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IProjectService _projectService;
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(
        ITicketService ticketService, 
        IProjectService projectService,
        IUserManagementService userManagementService,
        ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _projectService = projectService;
        _userManagementService = userManagementService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    private async Task<List<TicketAssignmentDto>> MapTicketAssignmentDtosAsync(IEnumerable<TicketManagement.Core.Entities.TicketAssignment> assignments)
    {
        if (!assignments.Any()) return new List<TicketAssignmentDto>();

        var assigneeIds = assignments.Select(a => a.AssigneeId).Distinct().ToList();
        var users = await _userManagementService.GetUsersByIdsAsync(assigneeIds);

        return assignments.Select(a => new TicketAssignmentDto
        {
            Id = a.Id,
            TicketId = a.TicketId,
            AssigneeId = a.AssigneeId,
            AssignedAt = a.AssignedAt,
            AssignedBy = a.AssignedBy,
            AssigneeName = users.TryGetValue(a.AssigneeId, out var user) ? user.DisplayName ?? user.Username : a.AssigneeId
        }).ToList();
    }

    /// <summary>
    /// プロジェクトのチケット一覧を検索・取得
    /// </summary>
    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<ApiResponseDto<PagedResultDto<TicketDto>>>> GetProjectTickets(
        Guid projectId, 
        [FromQuery] TicketSearchFilterDto filter)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.CanUserAccessProjectAsync(projectId, userId))
            {
                return Forbid();
            }

            var criteria = new TicketSearchCriteria
            {
                Keyword = filter.Keyword,
                Statuses = filter.Statuses,
                Priorities = filter.Priorities,
                Tags = filter.Tags,
                AssigneeIds = filter.AssigneeIds,
                CreatedAfter = filter.CreatedAfter,
                CreatedBefore = filter.CreatedBefore,
                DueAfter = filter.DueAfter,
                DueBefore = filter.DueBefore
            };

            var result = await _ticketService.SearchTicketsAsync(projectId, criteria, filter.Page, filter.PageSize);
            
            var ticketDtos = new List<TicketDto>();
            foreach (var ticket in result.Items)
            {
                var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);
                ticketDtos.Add(new TicketDto
                {
                    Id = ticket.Id,
                    ProjectId = ticket.ProjectId,
                    Title = ticket.Title,
                    Description = ticket.Description,
                    Status = ticket.Status,
                    Priority = ticket.Priority,
                    Category = ticket.Category,
                    Tags = ticket.Tags,
                    CreatedAt = ticket.CreatedAt,
                    CreatedBy = ticket.CreatedBy,
                    UpdatedAt = ticket.UpdatedAt,
                    UpdatedBy = ticket.UpdatedBy,
                    DueDate = ticket.DueDate,
                    Assignments = assignments,
                    CommentCount = ticket.Comments?.Count ?? 0,
                    ProjectName = ticket.Project?.Name ?? ""
                });
            }

            var pagedResult = new PagedResultDto<TicketDto>
            {
                Items = ticketDtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            return ApiResponseDto<PagedResultDto<TicketDto>>.SuccessResult(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tickets for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponseDto<PagedResultDto<TicketDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケット詳細を取得
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<TicketDetailDto>>> GetTicket(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                return Forbid();
            }

            var ticket = await _ticketService.GetTicketAsync(id);
            if (ticket == null)
            {
                return NotFound(ApiResponseDto<TicketDetailDto>.ErrorResult("Ticket not found"));
            }

            // Get user information for comments
            var commentUserIds = ticket.Comments?.Select(c => c.AuthorId).Distinct().ToList() ?? new List<string>();
            var users = await _userManagementService.GetUsersByIdsAsync(commentUserIds);

            // Map assignments with user names
            var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);

            var ticketDetailDto = new TicketDetailDto
            {
                Id = ticket.Id,
                ProjectId = ticket.ProjectId,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Category = ticket.Category,
                Tags = ticket.Tags,
                CreatedAt = ticket.CreatedAt,
                CreatedBy = ticket.CreatedBy,
                UpdatedAt = ticket.UpdatedAt,
                UpdatedBy = ticket.UpdatedBy,
                DueDate = ticket.DueDate,
                Assignments = assignments,
                CommentCount = ticket.Comments?.Count ?? 0,
                ProjectName = ticket.Project?.Name ?? "",
                Comments = ticket.Comments?.Select(c => new CommentDto
                {
                    Id = c.Id,
                    TicketId = c.TicketId,
                    Content = c.Content,
                    AuthorId = c.AuthorId,
                    CreatedBy = c.AuthorId,
                    AuthorName = users.TryGetValue(c.AuthorId, out var user) ? user.DisplayName : c.AuthorId,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    IsEdited = c.UpdatedAt.HasValue
                }).ToList() ?? new List<CommentDto>(),
                History = ticket.Histories?.Select(h => new TicketHistoryDto
                {
                    Id = h.Id,
                    TicketId = h.TicketId,
                    ChangedBy = h.ChangedBy,
                    ChangedAt = h.ChangedAt,
                    FieldName = h.FieldName,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    ActionType = h.ActionType
                }).ToList() ?? new List<TicketHistoryDto>()
            };

            return ApiResponseDto<TicketDetailDto>.SuccessResult(ticketDetailDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticket {TicketId}", id);
            return StatusCode(500, ApiResponseDto<TicketDetailDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 新規チケットを作成
    /// </summary>
    [HttpPost("project/{projectId:guid}")]
    public async Task<ActionResult<ApiResponseDto<TicketDto>>> CreateTicket(
        Guid projectId, 
        [FromBody] CreateTicketDto dto)
    {
        try
        {
            _logger.LogDebug("Creating ticket for project {ProjectId} with data: {@CreateTicketDto}", projectId, dto);
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                // Detailed validation error logging
                _logger.LogWarning("Ticket creation validation failed for project {ProjectId}. Errors: {Errors}. ModelState: {@ModelState}",
                    projectId, string.Join("; ", errors), ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage)));
                
                return BadRequest(ApiResponseDto<TicketDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            _logger.LogDebug("User {UserId} attempting to create ticket in project {ProjectId}", userId, projectId);
            
            if (!await _projectService.CanUserAccessProjectAsync(projectId, userId))
            {
                _logger.LogWarning("User {UserId} denied access to project {ProjectId} for ticket creation", userId, projectId);
                return Forbid();
            }

            var ticket = await _ticketService.CreateTicketAsync(
                projectId, 
                dto.Title, 
                dto.Description, 
                userId,
                dto.Priority,
                dto.Category,
                dto.Tags,
                dto.DueDate);

            var ticketDto = new TicketDto
            {
                Id = ticket.Id,
                ProjectId = ticket.ProjectId,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Category = ticket.Category,
                Tags = ticket.Tags,
                CreatedAt = ticket.CreatedAt,
                CreatedBy = ticket.CreatedBy,
                UpdatedAt = ticket.UpdatedAt,
                UpdatedBy = ticket.UpdatedBy,
                DueDate = ticket.DueDate,
                Assignments = new List<TicketAssignmentDto>(),
                CommentCount = 0,
                ProjectName = ticket.Project?.Name ?? ""
            };

            return ApiResponseDto<TicketDto>.SuccessResult(ticketDto, "Ticket created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket in project {ProjectId}", projectId);
            return StatusCode(500, ApiResponseDto<TicketDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケットを更新
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<TicketDto>>> UpdateTicket(
        Guid id, 
        [FromBody] UpdateTicketDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<TicketDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                return Forbid();
            }

            var ticket = await _ticketService.UpdateTicketAsync(
                id,
                dto.Title,
                dto.Description,
                dto.Priority,
                dto.Category,
                dto.Tags,
                dto.DueDate,
                userId);

            var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);

            var ticketDto = new TicketDto
            {
                Id = ticket.Id,
                ProjectId = ticket.ProjectId,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Category = ticket.Category,
                Tags = ticket.Tags,
                CreatedAt = ticket.CreatedAt,
                CreatedBy = ticket.CreatedBy,
                UpdatedAt = ticket.UpdatedAt,
                UpdatedBy = ticket.UpdatedBy,
                DueDate = ticket.DueDate,
                Assignments = assignments,
                CommentCount = ticket.Comments?.Count ?? 0,
                ProjectName = ticket.Project?.Name ?? ""
            };

            return ApiResponseDto<TicketDto>.SuccessResult(ticketDto, "Ticket updated successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<TicketDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket {TicketId}", id);
            return StatusCode(500, ApiResponseDto<TicketDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケットのステータスを更新
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponseDto<TicketDto>>> UpdateTicketStatus(
        Guid id, 
        [FromBody] UpdateTicketStatusDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<TicketDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                return Forbid();
            }

            var ticket = await _ticketService.UpdateTicketStatusAsync(id, dto.Status, userId);

            var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);

            var ticketDto = new TicketDto
            {
                Id = ticket.Id,
                ProjectId = ticket.ProjectId,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Category = ticket.Category,
                Tags = ticket.Tags,
                CreatedAt = ticket.CreatedAt,
                CreatedBy = ticket.CreatedBy,
                UpdatedAt = ticket.UpdatedAt,
                UpdatedBy = ticket.UpdatedBy,
                DueDate = ticket.DueDate,
                Assignments = assignments,
                CommentCount = ticket.Comments?.Count ?? 0,
                ProjectName = ticket.Project?.Name ?? ""
            };

            return ApiResponseDto<TicketDto>.SuccessResult(ticketDto, "Ticket status updated successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<TicketDto>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<TicketDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket status {TicketId}", id);
            return StatusCode(500, ApiResponseDto<TicketDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケットにアサインを追加
    /// </summary>
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Policy = "TicketAssignee")]
    public async Task<ActionResult<ApiResponseDto<TicketAssignmentDto>>> AssignTicket(
        Guid id, 
        [FromBody] AssignTicketDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<TicketAssignmentDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                return Forbid();
            }

            var ticket = await _ticketService.AssignTicketAsync(id, dto.AssigneeId, userId);

            var assignment = ticket.Assignments.LastOrDefault();
            if (assignment == null)
            {
                return StatusCode(500, ApiResponseDto<TicketAssignmentDto>.ErrorResult("Assignment creation failed"));
            }

            // Get user name for the assignment
            var users = await _userManagementService.GetUsersByIdsAsync(new[] { assignment.AssigneeId });
            var assigneeName = users.TryGetValue(assignment.AssigneeId, out var user) ? user.DisplayName ?? user.Username : assignment.AssigneeId;

            var assignmentDto = new TicketAssignmentDto
            {
                Id = assignment.Id,
                TicketId = assignment.TicketId,
                AssigneeId = assignment.AssigneeId,
                AssignedAt = assignment.AssignedAt,
                AssignedBy = assignment.AssignedBy,
                AssigneeName = assigneeName
            };

            return ApiResponseDto<TicketAssignmentDto>.SuccessResult(assignmentDto, "Ticket assigned successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<TicketAssignmentDto>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseDto<TicketAssignmentDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning ticket {TicketId}", id);
            return StatusCode(500, ApiResponseDto<TicketAssignmentDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケットをアサイン（簡易版）
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<ApiResponseDto<TicketAssignmentDto>>> AssignTicketSimple(
        Guid id, 
        [FromBody] AssignTicketDto dto)
    {
        try
        {
            _logger.LogInformation("AssignTicketSimple called for ticket {TicketId} with AssigneeId: {AssigneeId}", id, dto?.AssigneeId);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                _logger.LogWarning("AssignTicketSimple validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponseDto<TicketAssignmentDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            _logger.LogDebug("Current user: {UserId}", userId);
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                _logger.LogWarning("User {UserId} denied access to ticket {TicketId}", userId, id);
                return Forbid();
            }

            // 空のAssigneeIdの場合は全てのアサインを削除
            if (string.IsNullOrEmpty(dto.AssigneeId))
            {
                _logger.LogInformation("Removing all assignments from ticket {TicketId}", id);
                
                // 既存のアサインメントを全て削除
                var existingTicket = await _ticketService.GetTicketAsync(id);
                if (existingTicket?.Assignments?.Any() == true)
                {
                    _logger.LogDebug("Found {AssignmentCount} existing assignments", existingTicket.Assignments.Count());
                    foreach (var assignment in existingTicket.Assignments.ToList())
                    {
                        _logger.LogDebug("Removing assignment for assignee {AssigneeId}", assignment.AssigneeId);
                        await _ticketService.RemoveTicketAssignmentAsync(id, assignment.AssigneeId, userId);
                    }
                }
                
                _logger.LogInformation("All assignments removed from ticket {TicketId}", id);
                return ApiResponseDto<TicketAssignmentDto>.SuccessResult(new TicketAssignmentDto(), "All assignments removed successfully");
            }

            _logger.LogInformation("Assigning ticket {TicketId} to user {AssigneeId}", id, dto.AssigneeId);

            // 既存のアサインメントを削除してから新しいアサインメントを追加
            var ticket = await _ticketService.GetTicketAsync(id);
            if (ticket?.Assignments?.Any() == true)
            {
                _logger.LogDebug("Removing {AssignmentCount} existing assignments before new assignment", ticket.Assignments.Count());
                foreach (var assignment in ticket.Assignments.ToList())
                {
                    _logger.LogDebug("Removing existing assignment for assignee {AssigneeId}", assignment.AssigneeId);
                    await _ticketService.RemoveTicketAssignmentAsync(id, assignment.AssigneeId, userId);
                }
            }

            // 新しいアサインメントを追加
            _logger.LogDebug("Creating new assignment for ticket {TicketId} to user {AssigneeId}", id, dto.AssigneeId);
            var updatedTicket = await _ticketService.AssignTicketAsync(id, dto.AssigneeId, userId);

            var newAssignment = updatedTicket.Assignments.LastOrDefault();
            if (newAssignment == null)
            {
                _logger.LogError("Assignment creation failed for ticket {TicketId}", id);
                return StatusCode(500, ApiResponseDto<TicketAssignmentDto>.ErrorResult("Assignment creation failed"));
            }

            // Get user name for the assignment
            var users = await _userManagementService.GetUsersByIdsAsync(new[] { newAssignment.AssigneeId });
            var assigneeName = users.TryGetValue(newAssignment.AssigneeId, out var user) ? user.DisplayName ?? user.Username : newAssignment.AssigneeId;

            var assignmentDto = new TicketAssignmentDto
            {
                Id = newAssignment.Id,
                TicketId = newAssignment.TicketId,
                AssigneeId = newAssignment.AssigneeId,
                AssignedAt = newAssignment.AssignedAt,
                AssignedBy = newAssignment.AssignedBy,
                AssigneeName = assigneeName
            };

            _logger.LogInformation("Ticket {TicketId} successfully assigned to user {AssigneeId}", id, dto.AssigneeId);
            return ApiResponseDto<TicketAssignmentDto>.SuccessResult(assignmentDto, "Ticket assigned successfully");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "ArgumentException in AssignTicketSimple for ticket {TicketId}: {Message}", id, ex.Message);
            return NotFound(ApiResponseDto<TicketAssignmentDto>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "InvalidOperationException in AssignTicketSimple for ticket {TicketId}: {Message}", id, ex.Message);
            return BadRequest(ApiResponseDto<TicketAssignmentDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AssignTicketSimple for ticket {TicketId}: {Message}", id, ex.Message);
            return StatusCode(500, ApiResponseDto<TicketAssignmentDto>.ErrorResult($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// チケットからアサインを削除
    /// </summary>
    [HttpDelete("{id:guid}/assignments/{assigneeId}")]
    [Authorize(Policy = "TicketAssignee")]
    public async Task<ActionResult<ApiResponseDto<string>>> RemoveTicketAssignment(Guid id, string assigneeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(id, userId))
            {
                return Forbid();
            }

            await _ticketService.RemoveTicketAssignmentAsync(id, assigneeId, userId);

            return ApiResponseDto<string>.SuccessResult("success", "Assignment removed successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing assignment from ticket {TicketId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ユーザーが担当しているチケット一覧を取得
    /// </summary>
    [HttpGet("assigned")]
    public async Task<ActionResult<ApiResponseDto<List<TicketDto>>>> GetAssignedTickets()
    {
        try
        {
            var userId = GetCurrentUserId();
            var tickets = await _ticketService.GetTicketsByAssigneeAsync(userId);

            var ticketDtos = new List<TicketDto>();
            foreach (var ticket in tickets)
            {
                var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);
                ticketDtos.Add(new TicketDto
                {
                    Id = ticket.Id,
                    ProjectId = ticket.ProjectId,
                    Title = ticket.Title,
                    Description = ticket.Description,
                    Status = ticket.Status,
                    Priority = ticket.Priority,
                    Category = ticket.Category,
                    Tags = ticket.Tags,
                    CreatedAt = ticket.CreatedAt,
                    CreatedBy = ticket.CreatedBy,
                    UpdatedAt = ticket.UpdatedAt,
                    UpdatedBy = ticket.UpdatedBy,
                    DueDate = ticket.DueDate,
                    Assignments = assignments,
                    CommentCount = ticket.Comments?.Count ?? 0,
                    ProjectName = ticket.Project?.Name ?? ""
                });
            }

            return ApiResponseDto<List<TicketDto>>.SuccessResult(ticketDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assigned tickets for user");
            return StatusCode(500, ApiResponseDto<List<TicketDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 最近のチケット一覧を取得
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<ApiResponseDto<List<TicketDto>>>> GetRecentTickets([FromQuery] int count = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tickets = await _ticketService.GetRecentTicketsAsync(userId, count);

            var ticketDtos = new List<TicketDto>();
            foreach (var ticket in tickets)
            {
                var assignments = await MapTicketAssignmentDtosAsync(ticket.Assignments);
                ticketDtos.Add(new TicketDto
                {
                    Id = ticket.Id,
                    ProjectId = ticket.ProjectId,
                    Title = ticket.Title,
                    Description = ticket.Description,
                    Status = ticket.Status,
                    Priority = ticket.Priority,
                    Category = ticket.Category,
                    Tags = ticket.Tags,
                    CreatedAt = ticket.CreatedAt,
                    CreatedBy = ticket.CreatedBy,
                    UpdatedAt = ticket.UpdatedAt,
                    UpdatedBy = ticket.UpdatedBy,
                    DueDate = ticket.DueDate,
                    Assignments = assignments,
                    CommentCount = ticket.Comments?.Count ?? 0,
                    ProjectName = ticket.Project?.Name ?? ""
                });
            }

            return ApiResponseDto<List<TicketDto>>.SuccessResult(ticketDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent tickets for user");
            return StatusCode(500, ApiResponseDto<List<TicketDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// チケットを削除
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteTicket(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserDeleteTicketAsync(id, userId))
            {
                return Forbid();
            }

            await _ticketService.DeleteTicketAsync(id, userId);

            return ApiResponseDto<string>.SuccessResult("success", "Ticket deleted successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ticket {TicketId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }
}