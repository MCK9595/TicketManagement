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
}