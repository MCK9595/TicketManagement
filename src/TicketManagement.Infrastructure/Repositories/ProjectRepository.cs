using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class ProjectRepository : Repository<Project, Guid>, IProjectRepository
{
    private readonly ILogger<ProjectRepository> _logger;

    public ProjectRepository(TicketDbContext context, ILogger<ProjectRepository> logger) : base(context)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<Project>> GetProjectsByUserIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<Project>();
        }

        return await _context.Projects
            .Include(p => p.Members)
            .Include(p => p.Organization)
            .Where(p => p.IsActive && p.Members.Any(m => m.UserId == userId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetActiveProjectsAsync()
    {
        return await _context.Projects
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Projects
            .Where(p => p.OrganizationId == organizationId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectWithMembersAsync(Guid projectId)
    {
        return await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public async Task<Project?> GetProjectWithTicketsAsync(Guid projectId)
    {
        return await _context.Projects
            .Include(p => p.Tickets)
            .ThenInclude(t => t.Assignments)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public async Task<bool> IsUserMemberOfProjectAsync(Guid projectId, string userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
    }

    public async Task<IEnumerable<ProjectMember>> GetProjectMembersAsync(Guid projectId)
    {
        return await _context.ProjectMembers
            .Include(pm => pm.Project)
            .Where(pm => pm.ProjectId == projectId)
            .ToListAsync();
    }

    public override async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ProjectMember> AddProjectMemberAsync(ProjectMember member)
    {
        _context.ProjectMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task<ProjectMember> UpdateProjectMemberAsync(ProjectMember member)
    {
        _context.ProjectMembers.Update(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task RemoveProjectMemberAsync(Guid projectId, string userId)
    {
        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        
        if (member != null)
        {
            _context.ProjectMembers.Remove(member);
            await _context.SaveChangesAsync();
        }
    }
}