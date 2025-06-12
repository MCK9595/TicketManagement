using System.ComponentModel.DataAnnotations;
using TicketManagement.Contracts.Validation;

namespace TicketManagement.Contracts.DTOs;

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
    public string AuthorName { get; set; } = string.Empty; // Keycloakから取得
    public string TicketTitle { get; set; } = string.Empty;
}

public class CreateCommentDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    [SafeHtml]
    public string Content { get; set; } = string.Empty;
}

public class UpdateCommentDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    [SafeHtml]
    public string Content { get; set; } = string.Empty;
}