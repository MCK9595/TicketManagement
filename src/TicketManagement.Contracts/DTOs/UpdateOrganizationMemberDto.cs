using System.ComponentModel.DataAnnotations;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

public class UpdateOrganizationMemberDto
{
    [Required]
    public OrganizationRole Role { get; set; }
}