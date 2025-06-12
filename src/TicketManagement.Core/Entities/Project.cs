namespace TicketManagement.Core.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty; // Keycloak UserId
    public bool IsActive { get; set; }
    
    // Navigation
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}