using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    private readonly TicketDbContext _context;

    public OrganizationRepository(TicketDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByNameAsync(string name)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Name == name && o.IsActive);
    }

    public async Task<Organization?> GetByIdWithMembersAsync(Guid organizationId)
    {
        return await _context.Organizations
            .Include(o => o.Members.Where(m => m.IsActive))
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.IsActive);
    }

    public async Task<Organization?> GetByIdWithProjectsAsync(Guid organizationId)
    {
        return await _context.Organizations
            .Include(o => o.Projects.Where(p => p.IsActive))
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.IsActive);
    }

    public async Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId)
    {
        return await _context.Organizations
            .Where(o => o.IsActive && o.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderBy(o => o.Name)
            .ToListAsync();
    }

    public async Task<bool> IsUserMemberOfOrganizationAsync(Guid organizationId, string userId)
    {
        return await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId 
                          && m.UserId == userId 
                          && m.IsActive);
    }

    public async Task<bool> IsUserAdminOfOrganizationAsync(Guid organizationId, string userId)
    {
        return await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId 
                          && m.UserId == userId 
                          && m.Role == Core.Enums.OrganizationRole.Admin 
                          && m.IsActive);
    }

    public async Task<bool> IsUserManagerOfOrganizationAsync(Guid organizationId, string userId)
    {
        return await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId 
                          && m.UserId == userId 
                          && (m.Role == Core.Enums.OrganizationRole.Manager || m.Role == Core.Enums.OrganizationRole.Admin)
                          && m.IsActive);
    }

    public async Task<int> GetProjectCountAsync(Guid organizationId)
    {
        return await _context.Projects
            .CountAsync(p => p.OrganizationId == organizationId && p.IsActive);
    }

    public async Task<int> GetMemberCountAsync(Guid organizationId)
    {
        return await _context.OrganizationMembers
            .CountAsync(m => m.OrganizationId == organizationId && m.IsActive);
    }
}