namespace TicketManagement.Core.Entities;

public class TicketAssignment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string AssigneeId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    
    // Navigation
    public Ticket Ticket { get; set; } = null!;
}