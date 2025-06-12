namespace TicketManagement.Core.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
    
    // Navigation
    public Ticket Ticket { get; set; } = null!;

    // Business logic methods
    public void UpdateContent(string newContent, string userId)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Content cannot be empty", nameof(newContent));

        if (!CanEdit(userId))
            throw new UnauthorizedAccessException("User cannot edit this comment");

        if (Content != newContent)
        {
            Content = newContent;
            UpdatedAt = DateTime.UtcNow;
            IsEdited = true;
        }
    }

    public bool CanEdit(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && AuthorId == userId;
    }
}