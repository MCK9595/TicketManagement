using TicketManagement.Core.Enums;

namespace TicketManagement.Core.Entities;

public class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty; // Keycloak UserId
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    
    // Navigation
    public Project Project { get; set; } = null!;
}