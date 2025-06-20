using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using Microsoft.Extensions.Logging;

namespace TicketManagement.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationService _organizationService;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IProjectRepository projectRepository, 
        IOrganizationService organizationService,
        INotificationService notificationService, 
        ICacheService cacheService, 
        ILogger<ProjectService> logger)
    {
        _projectRepository = projectRepository;
        _organizationService = organizationService;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Project> CreateProjectAsync(Guid organizationId, string name, string description, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));
        
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        // Check if user can create project in this organization
        if (!await _organizationService.CanUserCreateProjectAsync(organizationId, createdBy))
            throw new UnauthorizedAccessException("User does not have permission to create projects in this organization");

        // Check if organization can add more projects
        if (!await _organizationService.CanCreateProjectAsync(organizationId))
            throw new InvalidOperationException("Organization has reached its project limit");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsActive = true
        };

        // 作成者を管理者として追加（プロジェクト作成と同時に）
        var adminMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = createdBy,
            Role = ProjectRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        project.Members.Add(adminMember);
        var createdProject = await _projectRepository.AddAsync(project);
        
        _logger.LogInformation("Project created: {ProjectId} in organization {OrganizationId} by user {UserId}", 
            createdProject.Id, organizationId, createdBy);

        // キャッシュを無効化
        var cacheKey = CacheKeys.UserProjects(createdBy);
        await _cacheService.RemoveAsync(cacheKey);
        _logger.LogDebug("Invalidated cache for user {UserId}: {CacheKey}", createdBy, cacheKey);

        return createdProject;
    }

    public async Task<Project> UpdateProjectAsync(Guid projectId, string name, string description, string updatedBy)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        project.Name = name;
        project.Description = description;

        var updatedProject = await _projectRepository.UpdateAsync(project);

        // キャッシュを無効化
        await _cacheService.RemoveAsync(CacheKeys.Project(projectId));
        
        return updatedProject;
    }

    public async Task<Project?> GetProjectAsync(Guid projectId)
    {
        var cacheKey = CacheKeys.Project(projectId);
        var cachedProject = await _cacheService.GetAsync<Project>(cacheKey);
        
        if (cachedProject != null)
        {
            return cachedProject;
        }

        var project = await _projectRepository.GetProjectWithMembersAsync(projectId);
        if (project != null)
        {
            await _cacheService.SetAsync(cacheKey, project, TimeSpan.FromMinutes(30));
        }

        return project;
    }

    public async Task<IEnumerable<Project>> GetProjectsByUserAsync(string userId)
    {
        var cacheKey = CacheKeys.UserProjects(userId);
        var cachedProjects = await _cacheService.GetAsync<IEnumerable<Project>>(cacheKey);
        
        if (cachedProjects != null)
        {
            _logger.LogDebug("Returning cached projects for user {UserId}: {ProjectCount} projects", userId, cachedProjects.Count());
            return cachedProjects;
        }

        _logger.LogDebug("Cache miss for user {UserId}, querying database", userId);
        var projects = await _projectRepository.GetProjectsByUserIdAsync(userId);
        _logger.LogInformation("Retrieved {ProjectCount} projects from database for user {UserId}", projects.Count(), userId);
        
        await _cacheService.SetAsync(cacheKey, projects, TimeSpan.FromMinutes(15));

        return projects;
    }

    public async Task<IEnumerable<Project>> GetActiveProjectsAsync()
    {
        return await _projectRepository.GetActiveProjectsAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByOrganizationAsync(Guid organizationId)
    {
        var cacheKey = $"org-projects:{organizationId}";
        var cachedProjects = await _cacheService.GetAsync<IEnumerable<Project>>(cacheKey);
        
        if (cachedProjects != null)
        {
            return cachedProjects;
        }

        var projects = await _projectRepository.GetProjectsByOrganizationIdAsync(organizationId);
        await _cacheService.SetAsync(cacheKey, projects, TimeSpan.FromMinutes(15));

        return projects;
    }

    public async Task<ProjectMember> AddMemberAsync(Guid projectId, string userId, ProjectRole role, string addedBy)
    {
        // 既にメンバーかチェック
        var isAlreadyMember = await _projectRepository.IsUserMemberOfProjectAsync(projectId, userId);
        if (isAlreadyMember)
        {
            throw new InvalidOperationException($"User {userId} is already a member of project {projectId}");
        }

        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        // ProjectMemberを直接追加する方法が必要なので、
        // 実際にはDbContextを通じて追加する必要があります
        // ここでは簡易実装として、ProjectRepositoryに追加メソッドが必要です
        
        var project = await _projectRepository.GetProjectWithMembersAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        project.Members.Add(member);
        await _projectRepository.UpdateAsync(project);

        // 新メンバーに通知
        await _notificationService.CreateNotificationAsync(
            userId,
            "Added to Project",
            $"You have been added to project '{project.Name}' as {role}",
            NotificationType.TicketAssigned); // 適切な通知タイプが必要

        return member;
    }

    public async Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, string userId, ProjectRole newRole, string updatedBy)
    {
        var members = await _projectRepository.GetProjectMembersAsync(projectId);
        var member = members.FirstOrDefault(m => m.UserId == userId);
        
        if (member == null)
        {
            throw new ArgumentException($"User {userId} is not a member of project {projectId}");
        }

        member.Role = newRole;

        var project = await _projectRepository.GetProjectWithMembersAsync(projectId);
        await _projectRepository.UpdateAsync(project);

        // メンバーに通知
        await _notificationService.CreateNotificationAsync(
            userId,
            "Role Updated",
            $"Your role in project '{project.Name}' has been updated to {newRole}",
            NotificationType.TicketAssigned);

        return member;
    }

    public async Task RemoveMemberAsync(Guid projectId, string userId, string removedBy)
    {
        var project = await _projectRepository.GetProjectWithMembersAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            throw new ArgumentException($"User {userId} is not a member of project {projectId}");
        }

        project.Members.Remove(member);
        await _projectRepository.UpdateAsync(project);

        // 削除されたメンバーに通知
        await _notificationService.CreateNotificationAsync(
            userId,
            "Removed from Project",
            $"You have been removed from project '{project.Name}'",
            NotificationType.TicketAssigned);
    }

    public async Task<IEnumerable<ProjectMember>> GetProjectMembersAsync(Guid projectId)
    {
        return await _projectRepository.GetProjectMembersAsync(projectId);
    }

    public async Task<bool> IsUserMemberOfProjectAsync(Guid projectId, string userId)
    {
        return await _projectRepository.IsUserMemberOfProjectAsync(projectId, userId);
    }

    public async Task<ProjectRole?> GetUserRoleInProjectAsync(Guid projectId, string userId)
    {
        var members = await _projectRepository.GetProjectMembersAsync(projectId);
        var member = members.FirstOrDefault(m => m.UserId == userId);
        return member?.Role;
    }

    public async Task DeactivateProjectAsync(Guid projectId, string deactivatedBy)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        project.IsActive = false;
        await _projectRepository.UpdateAsync(project);
    }

    public async Task ActivateProjectAsync(Guid projectId, string activatedBy)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        project.IsActive = true;
        await _projectRepository.UpdateAsync(project);
    }

    public async Task<bool> CanUserAccessProjectAsync(Guid projectId, string userId)
    {
        return await _projectRepository.IsUserMemberOfProjectAsync(projectId, userId);
    }

    public async Task<bool> CanUserManageProjectAsync(Guid projectId, string userId)
    {
        var role = await GetUserRoleInProjectAsync(projectId, userId);
        return role == ProjectRole.Admin;
    }

    public async Task DeleteProjectAsync(Guid projectId, string deletedBy)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));
        }

        // プロジェクトのメンバーに削除通知を送信
        var members = await _projectRepository.GetProjectMembersAsync(projectId);
        foreach (var member in members)
        {
            if (member.UserId != deletedBy) // 削除者本人には通知しない
            {
                await _notificationService.CreateNotificationAsync(
                    member.UserId,
                    "Project Deleted",
                    $"Project '{project.Name}' has been deleted",
                    NotificationType.ProjectDeleted);
            }
        }

        // プロジェクトを削除（カスケード削除でチケット、メンバーなども削除される）
        await _projectRepository.DeleteAsync(projectId);

        // 関連キャッシュを無効化
        await _cacheService.RemoveAsync(CacheKeys.Project(projectId));
        foreach (var member in members)
        {
            await _cacheService.RemoveAsync(CacheKeys.UserProjects(member.UserId));
        }

        _logger.LogInformation("Project deleted: {ProjectId} by user {UserId}", projectId, deletedBy);
    }
}