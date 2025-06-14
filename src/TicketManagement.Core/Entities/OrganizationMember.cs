using TicketManagement.Core.Enums;

namespace TicketManagement.Core.Entities;

public class OrganizationMember
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public OrganizationRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? InvitedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastAccessedAt { get; set; }
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
}