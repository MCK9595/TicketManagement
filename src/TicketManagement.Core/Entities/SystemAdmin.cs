using System.ComponentModel.DataAnnotations;

namespace TicketManagement.Core.Entities;

public class SystemAdmin
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserEmail { get; set; }

    public DateTime GrantedAt { get; set; }

    [Required]
    [StringLength(450)]
    public string GrantedBy { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Reason { get; set; }
}