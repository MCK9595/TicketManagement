using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class TicketAssignmentRepository : Repository<TicketAssignment, Guid>, ITicketAssignmentRepository
{
    public TicketAssignmentRepository(TicketDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TicketAssignment>> GetAssignmentsByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketAssignments
            .Include(a => a.Ticket)
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketAssignment>> GetAssignmentsByAssigneeIdAsync(string assigneeId)
    {
        return await _context.TicketAssignments
            .Include(a => a.Ticket)
            .ThenInclude(t => t.Project)
            .Where(a => a.AssigneeId == assigneeId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<TicketAssignment?> GetActiveAssignmentAsync(Guid ticketId, string assigneeId)
    {
        return await _context.TicketAssignments
            .Include(a => a.Ticket)
            .FirstOrDefaultAsync(a => a.TicketId == ticketId && a.AssigneeId == assigneeId);
    }

    public async Task<bool> IsTicketAssignedToUserAsync(Guid ticketId, string userId)
    {
        return await _context.TicketAssignments
            .AnyAsync(a => a.TicketId == ticketId && a.AssigneeId == userId);
    }

    public async Task RemoveAssignmentAsync(Guid ticketId, string assigneeId)
    {
        var assignment = await _context.TicketAssignments
            .FirstOrDefaultAsync(a => a.TicketId == ticketId && a.AssigneeId == assigneeId);
        
        if (assignment != null)
        {
            _context.TicketAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }
}