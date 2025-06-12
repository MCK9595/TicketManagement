using TicketManagement.Core.Entities;

namespace TicketManagement.Contracts.Repositories;

public interface ICommentRepository : IRepository<Comment, Guid>
{
    Task<IEnumerable<Comment>> GetCommentsByTicketIdAsync(Guid ticketId);
    Task<IEnumerable<Comment>> GetCommentsByAuthorAsync(string authorId);
    Task<Comment?> GetCommentWithTicketAsync(Guid commentId);
    Task<int> GetCommentCountByTicketIdAsync(Guid ticketId);
}