using TicketManagement.Core.Enums;

namespace TicketManagement.Core.Entities;

public class TicketHistory
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public HistoryActionType ActionType { get; set; }
    
    // Navigation
    public Ticket Ticket { get; set; } = null!;
}