using System.ComponentModel.DataAnnotations;

namespace TicketManagement.Contracts.DTOs;

public class UpdateOrganizationDto
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Organization name can only contain letters, numbers, hyphens, and underscores")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DisplayName { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}