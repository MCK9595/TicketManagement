namespace TicketManagement.Contracts.DTOs;

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsActive { get; set; }
    public int MaxProjects { get; set; }
    public int MaxMembers { get; set; }
    public int CurrentProjects { get; set; }
    public int CurrentMembers { get; set; }
    public List<OrganizationMemberDto> Members { get; set; } = new();
}