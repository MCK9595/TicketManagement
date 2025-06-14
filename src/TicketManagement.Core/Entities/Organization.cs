namespace TicketManagement.Core.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Settings
    public int MaxProjects { get; set; } = 100;
    public int MaxMembers { get; set; } = 1000;
    public string? BillingPlan { get; set; }
    public DateTime? BillingExpiresAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}