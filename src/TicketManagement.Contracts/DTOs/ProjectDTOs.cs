using TicketManagement.Core.Enums;
using System.ComponentModel.DataAnnotations;
using TicketManagement.Contracts.Validation;

namespace TicketManagement.Contracts.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<ProjectMemberDto> Members { get; set; } = new();
    public int TicketCount { get; set; }
}

public class CreateProjectDto
{
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    [ProjectName]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [SafeHtml]
    public string Description { get; set; } = string.Empty;
}

public class UpdateProjectDto
{
    [Required]
    [ProjectName]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [SafeHtml]
    public string Description { get; set; } = string.Empty;
}

public class ProjectMemberDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public string UserName { get; set; } = string.Empty; // Keycloakから取得
    public string Email { get; set; } = string.Empty; // Keycloakから取得
}

public class AddProjectMemberDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public ProjectRole Role { get; set; }
}

public class UpdateProjectMemberDto
{
    [Required]
    public ProjectRole Role { get; set; }
}

public class ProjectSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int MemberCount { get; set; }
    public DateTime LastActivity { get; set; }
}