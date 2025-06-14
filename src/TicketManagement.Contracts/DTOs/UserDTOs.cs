using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class UserDetailDto : UserDto
{
    public List<UserOrganizationDto> Organizations { get; set; } = new();
    public List<UserProjectDto> Projects { get; set; } = new();
    public int TotalTicketsAssigned { get; set; }
    public int ActiveTicketsAssigned { get; set; }
}

public class UserOrganizationDto
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string? OrganizationDisplayName { get; set; }
    public OrganizationRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserProjectDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SearchUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserSearchCriteria
{
    public string? SearchTerm { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsActive { get; set; }
    public int MaxResults { get; set; } = 20;
}

public class CreateUserInvitationDto
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    public string? InvitationMessage { get; set; }
}