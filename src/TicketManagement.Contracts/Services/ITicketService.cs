using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;
using TicketManagement.Contracts.Repositories;

namespace TicketManagement.Contracts.Services;

public interface ITicketService
{
    Task<Ticket> CreateTicketAsync(Guid projectId, string title, string description, string createdBy, TicketPriority priority = TicketPriority.Medium, string category = "", string[] tags = null, DateTime? dueDate = null);
    Task<Ticket> UpdateTicketAsync(Guid ticketId, string title, string description, TicketPriority priority, string category, string[] tags, DateTime? dueDate, string updatedBy);
    Task<Ticket> UpdateTicketStatusAsync(Guid ticketId, TicketStatus newStatus, string updatedBy);
    Task<Ticket> AssignTicketAsync(Guid ticketId, string assigneeId, string assignedBy);
    Task RemoveTicketAssignmentAsync(Guid ticketId, string assigneeId, string removedBy);
    Task<Comment> AddCommentAsync(Guid ticketId, string content, string authorId);
    Task<Comment> UpdateCommentAsync(Guid commentId, string content, string authorId);
    Task DeleteCommentAsync(Guid commentId, string authorId);
    Task<Ticket?> GetTicketAsync(Guid ticketId);
    Task<IEnumerable<Ticket>> GetTicketsByProjectAsync(Guid projectId);
    Task<IEnumerable<Ticket>> GetTicketsByAssigneeAsync(string assigneeId);
    Task<PagedResult<Ticket>> SearchTicketsAsync(Guid projectId, TicketSearchCriteria criteria, int page = 1, int pageSize = 20);
    Task<IEnumerable<Ticket>> GetRecentTicketsAsync(string userId, int count = 10);
    Task DeleteTicketAsync(Guid ticketId, string deletedBy);
    Task<bool> CanUserAccessTicketAsync(Guid ticketId, string userId);
    Task<bool> CanUserDeleteTicketAsync(Guid ticketId, string userId);
}