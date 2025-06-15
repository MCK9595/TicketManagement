using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class OrganizationMemberRepository : Repository<OrganizationMember, Guid>, IOrganizationMemberRepository
{
    private readonly TicketDbContext _context;

    public OrganizationMemberRepository(TicketDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId)
    {
        return await _context.OrganizationMembers
            .FirstOrDefaultAsync(om => om.OrganizationId == organizationId && om.UserId == userId);
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.OrganizationId == organizationId)
            .OrderBy(om => om.UserName)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationMember>> GetActiveOrganizationMembersAsync(Guid organizationId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.OrganizationId == organizationId && om.IsActive)
            .OrderBy(om => om.UserName)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationAdminsAsync(Guid organizationId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.OrganizationId == organizationId && 
                        om.Role == OrganizationRole.Admin && 
                        om.IsActive)
            .OrderBy(om => om.UserName)
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationManagersAsync(Guid organizationId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.OrganizationId == organizationId && 
                        (om.Role == OrganizationRole.Manager || om.Role == OrganizationRole.Admin) && 
                        om.IsActive)
            .OrderBy(om => om.UserName)
            .ToListAsync();
    }

    public async Task<OrganizationRole?> GetUserRoleInOrganizationAsync(Guid organizationId, string userId)
    {
        var member = await _context.OrganizationMembers
            .FirstOrDefaultAsync(om => om.OrganizationId == organizationId && 
                                      om.UserId == userId && 
                                      om.IsActive);
        
        return member?.Role;
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, string userId)
    {
        var member = await GetMemberAsync(organizationId, userId);
        if (member == null) return false;

        member.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid organizationId, string userId, OrganizationRole newRole)
    {
        var member = await GetMemberAsync(organizationId, userId);
        if (member == null) return false;

        member.Role = newRole;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Organization>> GetUserOrganizationsWithRoleAsync(string userId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.UserId == userId && om.IsActive)
            .Include(om => om.Organization)
            .Select(om => om.Organization)
            .Where(o => o.IsActive)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<OrganizationMember>> GetUserOrganizationMembershipsAsync(string userId)
    {
        return await _context.OrganizationMembers
            .Where(om => om.UserId == userId && om.IsActive)
            .Include(om => om.Organization)
            .Where(om => om.Organization.IsActive)
            .OrderBy(om => om.Organization.Name)
            .ToListAsync();
    }

    public async Task<OrganizationMember?> GetByUserIdAndOrganizationIdAsync(string userId, Guid organizationId)
    {
        return await _context.OrganizationMembers
            .FirstOrDefaultAsync(om => om.UserId == userId && om.OrganizationId == organizationId);
    }
}