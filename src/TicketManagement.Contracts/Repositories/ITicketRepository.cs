using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Repositories;

public interface ITicketRepository : IRepository<Ticket, Guid>
{
    Task<IEnumerable<Ticket>> GetTicketsByProjectIdAsync(Guid projectId);
    Task<IEnumerable<Ticket>> GetTicketsByAssigneeAsync(string assigneeId);
    Task<IEnumerable<Ticket>> GetTicketsByStatusAsync(TicketStatus status);
    Task<IEnumerable<Ticket>> GetTicketsByPriorityAsync(TicketPriority priority);
    Task<Ticket?> GetTicketWithCommentsAsync(Guid ticketId);
    Task<Ticket?> GetTicketWithAssignmentsAsync(Guid ticketId);
    Task<Ticket?> GetTicketWithHistoryAsync(Guid ticketId);
    Task<PagedResult<Ticket>> SearchTicketsAsync(Guid projectId, TicketSearchCriteria criteria, int page, int pageSize);
    Task<IEnumerable<Ticket>> GetRecentTicketsAsync(string userId, int count = 10);
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class TicketSearchCriteria
{
    public string? Keyword { get; set; }
    public TicketStatus[]? Statuses { get; set; }
    public TicketPriority[]? Priorities { get; set; }
    public string[]? Tags { get; set; }
    public string[]? AssigneeIds { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? DueAfter { get; set; }
    public DateTime? DueBefore { get; set; }
}