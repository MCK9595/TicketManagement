using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

public class OrganizationMemberDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public OrganizationRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? InvitedBy { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}