using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class TicketHistoryRepository : Repository<TicketHistory, Guid>, ITicketHistoryRepository
{
    public TicketHistoryRepository(TicketDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TicketHistory>> GetHistoryByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketHistories
            .Include(h => h.Ticket)
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketHistory>> GetHistoryByUserIdAsync(string userId)
    {
        return await _context.TicketHistories
            .Include(h => h.Ticket)
            .ThenInclude(t => t.Project)
            .Where(h => h.ChangedBy == userId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketHistory>> GetHistoryByActionTypeAsync(Guid ticketId, HistoryActionType actionType)
    {
        return await _context.TicketHistories
            .Include(h => h.Ticket)
            .Where(h => h.TicketId == ticketId && h.ActionType == actionType)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<TicketHistory> AddHistoryAsync(
        Guid ticketId, 
        string userId, 
        string fieldName, 
        string? oldValue, 
        string? newValue, 
        HistoryActionType actionType)
    {
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ActionType = actionType
        };

        await _context.TicketHistories.AddAsync(history);
        await _context.SaveChangesAsync();
        
        return history;
    }
}