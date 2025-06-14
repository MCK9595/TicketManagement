using System.ComponentModel.DataAnnotations;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

public class AddOrganizationMemberDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    public string? UserEmail { get; set; }

    [Required]
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
}