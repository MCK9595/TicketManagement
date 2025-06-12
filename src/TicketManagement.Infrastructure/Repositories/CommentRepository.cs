using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class CommentRepository : Repository<Comment, Guid>, ICommentRepository
{
    public CommentRepository(TicketDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Comment>> GetCommentsByTicketIdAsync(Guid ticketId)
    {
        return await _context.Comments
            .Include(c => c.Ticket)
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Comment>> GetCommentsByAuthorAsync(string authorId)
    {
        return await _context.Comments
            .Include(c => c.Ticket)
            .ThenInclude(t => t.Project)
            .Where(c => c.AuthorId == authorId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment?> GetCommentWithTicketAsync(Guid commentId)
    {
        return await _context.Comments
            .Include(c => c.Ticket)
            .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(c => c.Id == commentId);
    }

    public async Task<int> GetCommentCountByTicketIdAsync(Guid ticketId)
    {
        return await _context.Comments
            .CountAsync(c => c.TicketId == ticketId);
    }
}