using TicketManagement.Core.Entities;

namespace TicketManagement.Contracts.Repositories;

public interface ITicketAssignmentRepository : IRepository<TicketAssignment, Guid>
{
    Task<IEnumerable<TicketAssignment>> GetAssignmentsByTicketIdAsync(Guid ticketId);
    Task<IEnumerable<TicketAssignment>> GetAssignmentsByAssigneeIdAsync(string assigneeId);
    Task<TicketAssignment?> GetActiveAssignmentAsync(Guid ticketId, string assigneeId);
    Task<bool> IsTicketAssignedToUserAsync(Guid ticketId, string userId);
    Task RemoveAssignmentAsync(Guid ticketId, string assigneeId);
}