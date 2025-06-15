using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Repositories;

public interface IOrganizationMemberRepository : IRepository<OrganizationMember, Guid>
{
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId);
    Task<IEnumerable<OrganizationMember>> GetActiveOrganizationMembersAsync(Guid organizationId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationAdminsAsync(Guid organizationId);
    Task<IEnumerable<OrganizationMember>> GetOrganizationManagersAsync(Guid organizationId);
    Task<OrganizationRole?> GetUserRoleInOrganizationAsync(Guid organizationId, string userId);
    Task<bool> RemoveMemberAsync(Guid organizationId, string userId);
    Task<bool> UpdateMemberRoleAsync(Guid organizationId, string userId, OrganizationRole newRole);
    Task<IEnumerable<Organization>> GetUserOrganizationsWithRoleAsync(string userId);
    Task<IEnumerable<OrganizationMember>> GetUserOrganizationMembershipsAsync(string userId);
    Task<OrganizationMember?> GetByUserIdAndOrganizationIdAsync(string userId, Guid organizationId);
}