using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Services;

public interface IOrganizationService
{
    // Organization Management
    Task<Organization> CreateOrganizationAsync(string name, string? displayName, string? description, string createdBy);
    Task<Organization> UpdateOrganizationAsync(Guid organizationId, string name, string? displayName, string? description, string updatedBy);
    Task DeleteOrganizationAsync(Guid organizationId, string deletedBy);
    Task<Organization?> GetOrganizationAsync(Guid organizationId);
    Task<Organization?> GetOrganizationWithDetailsAsync(Guid organizationId);
    Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationMembersForUserAsync(string userId);
    
    // Member Management
    Task<OrganizationMember> AddMemberAsync(Guid organizationId, string userId, string userName, string? userEmail, OrganizationRole role, string invitedBy);
    Task<OrganizationMember> UpdateMemberRoleAsync(Guid organizationId, string userId, OrganizationRole newRole, string updatedBy);
    Task RemoveMemberAsync(Guid organizationId, string userId, string removedBy);
    Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId);
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId);
    
    // Permission Checks
    Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, string userId);
    Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId);
    Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId);
    Task<bool> CanUserManageMembersAsync(Guid organizationId, string userId);
    Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId);
    
    // Organization Limits
    Task<bool> CanCreateProjectAsync(Guid organizationId);
    Task<bool> CanAddMemberAsync(Guid organizationId);
    Task<(int current, int max)> GetProjectLimitsAsync(Guid organizationId);
    Task<(int current, int max)> GetMemberLimitsAsync(Guid organizationId);
}