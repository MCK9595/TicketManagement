using TicketManagement.Core.Entities;

namespace TicketManagement.Contracts.Repositories;

public interface IProjectRepository : IRepository<Project, Guid>
{
    Task<IEnumerable<Project>> GetProjectsByUserIdAsync(string userId);
    Task<IEnumerable<Project>> GetProjectsByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<Project>> GetActiveProjectsAsync();
    Task<Project?> GetProjectWithMembersAsync(Guid projectId);
    Task<Project?> GetProjectWithTicketsAsync(Guid projectId);
    Task<bool> IsUserMemberOfProjectAsync(Guid projectId, string userId);
    Task<IEnumerable<ProjectMember>> GetProjectMembersAsync(Guid projectId);
    Task<ProjectMember> AddProjectMemberAsync(ProjectMember member);
    Task<ProjectMember> UpdateProjectMemberAsync(ProjectMember member);
    Task RemoveProjectMemberAsync(Guid projectId, string userId);
}