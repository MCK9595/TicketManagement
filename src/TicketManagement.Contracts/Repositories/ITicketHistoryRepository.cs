using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Repositories;

public interface ITicketHistoryRepository : IRepository<TicketHistory, Guid>
{
    Task<IEnumerable<TicketHistory>> GetHistoryByTicketIdAsync(Guid ticketId);
    Task<IEnumerable<TicketHistory>> GetHistoryByUserIdAsync(string userId);
    Task<IEnumerable<TicketHistory>> GetHistoryByActionTypeAsync(Guid ticketId, HistoryActionType actionType);
    Task<TicketHistory> AddHistoryAsync(Guid ticketId, string userId, string fieldName, string? oldValue, string? newValue, HistoryActionType actionType);
}