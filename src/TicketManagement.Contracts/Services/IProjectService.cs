using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(Guid organizationId, string name, string description, string createdBy);
    Task<Project> UpdateProjectAsync(Guid projectId, string name, string description, string updatedBy);
    Task<Project?> GetProjectAsync(Guid projectId);
    Task<IEnumerable<Project>> GetProjectsByUserAsync(string userId);
    Task<IEnumerable<Project>> GetProjectsByOrganizationAsync(Guid organizationId);
    Task<IEnumerable<Project>> GetActiveProjectsAsync();
    Task<ProjectMember> AddMemberAsync(Guid projectId, string userId, ProjectRole role, string addedBy);
    Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, string userId, ProjectRole newRole, string updatedBy);
    Task RemoveMemberAsync(Guid projectId, string userId, string removedBy);
    Task<IEnumerable<ProjectMember>> GetProjectMembersAsync(Guid projectId);
    Task<bool> IsUserMemberOfProjectAsync(Guid projectId, string userId);
    Task<ProjectRole?> GetUserRoleInProjectAsync(Guid projectId, string userId);
    Task DeactivateProjectAsync(Guid projectId, string deactivatedBy);
    Task ActivateProjectAsync(Guid projectId, string activatedBy);
    Task<bool> CanUserAccessProjectAsync(Guid projectId, string userId);
    Task<bool> CanUserManageProjectAsync(Guid projectId, string userId);
    Task DeleteProjectAsync(Guid projectId, string deletedBy);
}