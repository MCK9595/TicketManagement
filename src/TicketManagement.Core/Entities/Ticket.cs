using TicketManagement.Core.Enums;

namespace TicketManagement.Core.Entities;

public class Ticket
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
    
    // Navigation
    public Project Project { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
    public ICollection<TicketHistory> Histories { get; set; } = new List<TicketHistory>();

    // Business logic methods
    public void UpdateStatus(TicketStatus newStatus, string userId)
    {
        if (Status != newStatus)
        {
            var oldStatus = Status;
            Status = newStatus;
            UpdatedBy = userId;
            UpdatedAt = DateTime.UtcNow;

            // Add history entry
            Histories.Add(new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = Id,
                FieldName = "Status",
                OldValue = oldStatus.ToString(),
                NewValue = newStatus.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                ActionType = HistoryActionType.Updated
            });
        }
    }

    public void UpdatePriority(TicketPriority newPriority, string userId)
    {
        if (Priority != newPriority)
        {
            var oldPriority = Priority;
            Priority = newPriority;
            UpdatedBy = userId;
            UpdatedAt = DateTime.UtcNow;

            // Add history entry
            Histories.Add(new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = Id,
                FieldName = "Priority",
                OldValue = oldPriority.ToString(),
                NewValue = newPriority.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                ActionType = HistoryActionType.Updated
            });
        }
    }

    public bool CanTransitionTo(TicketStatus newStatus)
    {
        // Define valid status transitions
        return Status switch
        {
            TicketStatus.Open => true, // Can transition to any status
            TicketStatus.InProgress => newStatus != TicketStatus.Open,
            TicketStatus.Review => newStatus == TicketStatus.InProgress || newStatus == TicketStatus.Closed,
            TicketStatus.Closed => newStatus == TicketStatus.Open, // Can only reopen
            TicketStatus.OnHold => newStatus != TicketStatus.Closed,
            _ => false
        };
    }

    public void AddComment(Comment comment)
    {
        comment.TicketId = Id;
        Comments.Add(comment);
        UpdatedAt = DateTime.UtcNow;
    }
}