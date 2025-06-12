using TicketManagement.Core.Enums;
using System.ComponentModel.DataAnnotations;
using TicketManagement.Contracts.Validation;

namespace TicketManagement.Contracts.DTOs;

public class TicketDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public string Category { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DueDate { get; set; }
    public List<TicketAssignmentDto> Assignments { get; set; } = new();
    public int CommentCount { get; set; }
    public string ProjectName { get; set; } = string.Empty;
}

public class TicketDetailDto : TicketDto
{
    public List<CommentDto> Comments { get; set; } = new();
    public List<TicketHistoryDto> History { get; set; } = new();
}

public class CreateTicketDto
{
    [Required]
    [TicketTitle]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [SafeHtml]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Tags]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [FutureDate]
    public DateTime? DueDate { get; set; }
}

public class UpdateTicketDto
{
    [Required]
    [TicketTitle]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [SafeHtml]
    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Tags]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [FutureDate]
    public DateTime? DueDate { get; set; }
}

public class UpdateTicketStatusDto
{
    [Required]
    public TicketStatus Status { get; set; }
}

public class TicketAssignmentDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string AssigneeId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty; // Keycloakから取得
}

public class AssignTicketDto
{
    [Required]
    public string AssigneeId { get; set; } = string.Empty;
}

public class TicketHistoryDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public HistoryActionType ActionType { get; set; }
    public string ChangedByName { get; set; } = string.Empty; // Keycloakから取得
}

public class TicketSearchFilterDto
{
    public string? Keyword { get; set; }
    public TicketStatus[]? Statuses { get; set; }
    public TicketPriority[]? Priorities { get; set; }
    public string[]? Tags { get; set; }
    public string[]? AssigneeIds { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? DueAfter { get; set; }
    public DateTime? DueBefore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}