using System.ComponentModel.DataAnnotations;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

public class CreateUserDto
{
    [Required]
    [StringLength(256)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(256)]
    public string? FirstName { get; set; }

    [StringLength(256)]
    public string? LastName { get; set; }

    [StringLength(100)]
    public string? TemporaryPassword { get; set; }

    public bool RequirePasswordChange { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public List<UserRoleAssignmentDto> RoleAssignments { get; set; } = new();
}

public class UserRoleAssignmentDto
{
    public Guid OrganizationId { get; set; }
    public OrganizationRole Role { get; set; }
    public string? InvitedBy { get; set; }
}

public class CreateUserResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? TemporaryPassword { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }
}

public class InviteUserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public OrganizationRole Role { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Message { get; set; }
}

public class InviteUserResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public bool UserAlreadyExists { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }
}

public class ResetPasswordResult
{
    public bool Success { get; set; }
    public string? TemporaryPassword { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }
}

public class GrantSystemAdminDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserEmail { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class SystemAdminDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public DateTime GrantedAt { get; set; }
    public string GrantedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class CreateUserForOrganizationDto
{
    [Required]
    [StringLength(256)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(256)]
    public string? FirstName { get; set; }

    [StringLength(256)]
    public string? LastName { get; set; }

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;

    public bool RequirePasswordChange { get; set; } = false;

    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public OrganizationRole Role { get; set; }
}

public class UpdateUserRoleDto
{
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public OrganizationRole NewRole { get; set; }
}