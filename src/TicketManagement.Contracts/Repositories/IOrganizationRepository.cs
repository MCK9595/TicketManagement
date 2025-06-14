using TicketManagement.Core.Entities;

namespace TicketManagement.Contracts.Repositories;

public interface IOrganizationRepository : IRepository<Organization, Guid>
{
    Task<Organization?> GetByNameAsync(string name);
    Task<Organization?> GetByIdWithMembersAsync(Guid organizationId);
    Task<Organization?> GetByIdWithProjectsAsync(Guid organizationId);
    Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId);
    Task<bool> IsUserMemberOfOrganizationAsync(Guid organizationId, string userId);
    Task<bool> IsUserAdminOfOrganizationAsync(Guid organizationId, string userId);
    Task<bool> IsUserManagerOfOrganizationAsync(Guid organizationId, string userId);
    Task<int> GetProjectCountAsync(Guid organizationId);
    Task<int> GetMemberCountAsync(Guid organizationId);
}