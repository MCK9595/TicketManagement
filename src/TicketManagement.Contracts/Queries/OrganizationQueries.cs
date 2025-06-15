using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Queries;

// Query interfaces for organization operations
public interface IOrganizationQueryService
{
    Task<Organization?> GetOrganizationAsync(Guid organizationId);
    Task<Organization?> GetOrganizationWithDetailsAsync(Guid organizationId);
    Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationMembersForUserAsync(string userId);
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId);
    Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId);
    Task<(int current, int max)> GetProjectLimitsAsync(Guid organizationId);
    Task<(int current, int max)> GetMemberLimitsAsync(Guid organizationId);
}

// Authorization query interface - separate concern
public interface IOrganizationAuthorizationService
{
    Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, string userId);
    Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId);
    Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId);
    Task<bool> CanUserManageMembersAsync(Guid organizationId, string userId);
    Task<bool> CanCreateProjectAsync(Guid organizationId);
    Task<bool> CanAddMemberAsync(Guid organizationId);
}

// Query result objects for better type safety
public record OrganizationProjectLimits(int Current, int Max);
public record OrganizationMemberLimits(int Current, int Max);

public record OrganizationMembershipInfo(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationDisplayName,
    OrganizationRole Role,
    DateTime JoinedAt,
    bool IsActive
);