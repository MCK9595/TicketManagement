using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class ProjectRepository : Repository<Project, Guid>, IProjectRepository
{
    public ProjectRepository(TicketDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Project>> GetProjectsByUserIdAsync(string userId)
    {
        return await _context.Projects
            .Include(p => p.Members)
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetActiveProjectsAsync()
    {
        return await _context.Projects
            .Where(p => p.IsActive)
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
}