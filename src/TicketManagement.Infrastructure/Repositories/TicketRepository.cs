using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class TicketRepository : Repository<Ticket, Guid>, ITicketRepository
{
    public TicketRepository(TicketDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByProjectIdAsync(Guid projectId)
    {
        return await _context.Tickets
            .Include(t => t.Assignments)
            .Include(t => t.Comments)
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByAssigneeAsync(string assigneeId)
    {
        return await _context.Tickets
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .Where(t => t.Assignments.Any(a => a.AssigneeId == assigneeId))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(TicketStatus status)
    {
        return await _context.Tickets
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(TicketPriority priority)
    {
        return await _context.Tickets
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .Where(t => t.Priority == priority)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicketWithCommentsAsync(Guid ticketId)
    {
        return await _context.Tickets
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    public async Task<Ticket?> GetTicketWithAssignmentsAsync(Guid ticketId)
    {
        return await _context.Tickets
            .Include(t => t.Assignments)
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    public async Task<Ticket?> GetTicketWithHistoryAsync(Guid ticketId)
    {
        return await _context.Tickets
            .Include(t => t.Histories.OrderByDescending(h => h.ChangedAt))
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    public async Task<PagedResult<Ticket>> SearchTicketsAsync(
        Guid projectId, 
        TicketSearchCriteria criteria, 
        int page, 
        int pageSize)
    {
        var query = _context.Tickets
            .Include(t => t.Assignments)
            .Include(t => t.Comments)
            .Where(t => t.ProjectId == projectId);

        // キーワード検索
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            query = query.Where(t => 
                t.Title.Contains(criteria.Keyword) || 
                t.Description.Contains(criteria.Keyword));
        }

        // ステータスフィルター
        if (criteria.Statuses?.Any() == true)
        {
            query = query.Where(t => criteria.Statuses.Contains(t.Status));
        }

        // 優先度フィルター
        if (criteria.Priorities?.Any() == true)
        {
            query = query.Where(t => criteria.Priorities.Contains(t.Priority));
        }

        // タグフィルター
        if (criteria.Tags?.Any() == true)
        {
            query = query.Where(t => 
                t.Tags.Any(tag => criteria.Tags.Contains(tag)));
        }

        // 担当者フィルター
        if (criteria.AssigneeIds?.Any() == true)
        {
            query = query.Where(t => 
                t.Assignments.Any(a => criteria.AssigneeIds.Contains(a.AssigneeId)));
        }

        // 作成日フィルター
        if (criteria.CreatedAfter.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= criteria.CreatedAfter.Value);
        }

        if (criteria.CreatedBefore.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= criteria.CreatedBefore.Value);
        }

        // 期限フィルター
        if (criteria.DueAfter.HasValue)
        {
            query = query.Where(t => t.DueDate >= criteria.DueAfter.Value);
        }

        if (criteria.DueBefore.HasValue)
        {
            query = query.Where(t => t.DueDate <= criteria.DueBefore.Value);
        }

        // 総数を取得
        var totalCount = await query.CountAsync();

        // ページング適用
        var items = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Ticket>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<Ticket>> GetRecentTicketsAsync(string userId, int count = 10)
    {
        return await _context.Tickets
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .Where(t => t.CreatedBy == userId || 
                       t.Assignments.Any(a => a.AssigneeId == userId))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public override async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _context.Tickets
            .Include(t => t.Project)
            .Include(t => t.Assignments)
            .Include(t => t.Comments)
            .Include(t => t.Histories)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByIdAsyncNoTracking(Guid id)
    {
        return await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByIdAsyncSimple(Guid id)
    {
        return await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public override async Task<Ticket> UpdateAsync(Ticket entity)
    {
        try
        {
            // Find the existing entity in the database
            var existingEntity = await _context.Tickets.FindAsync(entity.Id);
            
            if (existingEntity == null)
            {
                throw new InvalidOperationException($"Ticket with ID {entity.Id} not found in database");
            }

            // Update the existing entity with new values
            _context.Entry(existingEntity).CurrentValues.SetValues(entity);
            
            // Save changes
            await _context.SaveChangesAsync();
            
            return existingEntity;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Log the error and rethrow
            throw new InvalidOperationException($"Concurrency error updating ticket {entity.Id}", ex);
        }
    }

    public async Task<Ticket> UpdateStatusAsync(Guid ticketId, TicketStatus newStatus, string updatedBy)
    {
        const int maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                // Load ticket to check current status
                var ticket = await _context.Tickets
                    .Where(t => t.Id == ticketId)
                    .FirstOrDefaultAsync();

                if (ticket == null)
                {
                    throw new InvalidOperationException($"Ticket with ID {ticketId} not found");
                }

                var currentStatus = ticket.Status;
                var currentUpdatedAt = ticket.UpdatedAt;
                var now = DateTime.UtcNow;

                // Use raw SQL to update with optimistic concurrency check on status and UpdatedAt
                var affectedRows = await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE Tickets 
                    SET Status = {0}, UpdatedBy = {1}, UpdatedAt = {2}
                    WHERE Id = {3} AND Status = {4} AND (UpdatedAt = {5} OR UpdatedAt IS NULL)",
                    (int)newStatus, updatedBy, now, ticketId, (int)currentStatus, currentUpdatedAt);

                if (affectedRows == 0)
                {
                    if (retryCount < maxRetries - 1)
                    {
                        retryCount++;
                        await Task.Delay(100 * retryCount);
                        continue;
                    }
                    else
                    {
                        throw new InvalidOperationException("Ticket status has been modified by another user. Please refresh and try again.");
                    }
                }

                // Add history entry
                var historyEntry = new TicketHistory
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticketId,
                    FieldName = "Status",
                    OldValue = currentStatus.ToString(),
                    NewValue = newStatus.ToString(),
                    ChangedBy = updatedBy,
                    ChangedAt = now,
                    ActionType = HistoryActionType.Updated
                };

                _context.TicketHistories.Add(historyEntry);
                await _context.SaveChangesAsync();

                // Return updated ticket
                ticket.Status = newStatus;
                ticket.UpdatedBy = updatedBy;
                ticket.UpdatedAt = now;
                return ticket;
            }
            catch (Exception ex) when (!(ex is InvalidOperationException) && retryCount < maxRetries - 1)
            {
                retryCount++;
                _context.ChangeTracker.Clear();
                await Task.Delay(100 * retryCount);
            }
        }

        throw new InvalidOperationException($"Unable to update ticket status after {maxRetries} attempts due to concurrency conflicts. Please try again.");
    }
}