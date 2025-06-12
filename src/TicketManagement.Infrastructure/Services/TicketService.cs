using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITicketAssignmentRepository _assignmentRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly ITicketHistoryRepository _historyRepository;
    private readonly INotificationService _notificationService;

    public TicketService(
        ITicketRepository ticketRepository,
        IProjectRepository projectRepository,
        ITicketAssignmentRepository assignmentRepository,
        ICommentRepository commentRepository,
        ITicketHistoryRepository historyRepository,
        INotificationService notificationService)
    {
        _ticketRepository = ticketRepository;
        _projectRepository = projectRepository;
        _assignmentRepository = assignmentRepository;
        _commentRepository = commentRepository;
        _historyRepository = historyRepository;
        _notificationService = notificationService;
    }

    public async Task<Ticket> CreateTicketAsync(
        Guid projectId, 
        string title, 
        string description, 
        string createdBy, 
        TicketPriority priority = TicketPriority.Medium, 
        string category = "", 
        string[] tags = null, 
        DateTime? dueDate = null)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));
        
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        // Validate project exists
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new ArgumentException($"Project with ID {projectId} not found.", nameof(projectId));

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Description = description,
            Status = TicketStatus.Open,
            Priority = priority,
            Category = category,
            Tags = tags ?? Array.Empty<string>(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            DueDate = dueDate
        };

        var createdTicket = await _ticketRepository.AddAsync(ticket);
        
        // 履歴記録
        await _historyRepository.AddHistoryAsync(
            createdTicket.Id, 
            createdBy, 
            "Created", 
            null, 
            "Ticket created", 
            HistoryActionType.Created);

        return createdTicket;
    }

    public async Task<Ticket> UpdateTicketAsync(
        Guid ticketId, 
        string title, 
        string description, 
        TicketPriority priority, 
        string category, 
        string[] tags, 
        DateTime? dueDate, 
        string updatedBy)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            throw new ArgumentException($"Ticket with ID {ticketId} not found.", nameof(ticketId));
        }

        // 変更を追跡
        var changes = new List<(string field, string oldValue, string newValue)>();

        if (ticket.Title != title)
            changes.Add(("Title", ticket.Title, title));
        if (ticket.Description != description)
            changes.Add(("Description", ticket.Description, description));
        if (ticket.Priority != priority)
            changes.Add(("Priority", ticket.Priority.ToString(), priority.ToString()));
        if (ticket.Category != category)
            changes.Add(("Category", ticket.Category, category));
        if (!ticket.Tags.SequenceEqual(tags))
            changes.Add(("Tags", string.Join(",", ticket.Tags), string.Join(",", tags)));
        if (ticket.DueDate != dueDate)
            changes.Add(("DueDate", ticket.DueDate?.ToString(), dueDate?.ToString()));

        // 更新実行
        ticket.Title = title;
        ticket.Description = description;
        ticket.Priority = priority;
        ticket.Category = category;
        ticket.Tags = tags;
        ticket.DueDate = dueDate;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = updatedBy;

        var updatedTicket = await _ticketRepository.UpdateAsync(ticket);

        // 履歴記録
        foreach (var (field, oldValue, newValue) in changes)
        {
            await _historyRepository.AddHistoryAsync(
                ticketId, 
                updatedBy, 
                field, 
                oldValue, 
                newValue, 
                HistoryActionType.Updated);
        }

        return updatedTicket;
    }

    public async Task<Ticket> UpdateTicketStatusAsync(Guid ticketId, TicketStatus newStatus, string updatedBy)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            throw new ArgumentException($"Ticket with ID {ticketId} not found.", nameof(ticketId));
        }

        if (!ticket.CanTransitionTo(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition from {ticket.Status} to {newStatus}");
        }

        var oldStatus = ticket.Status;
        ticket.UpdateStatus(newStatus, updatedBy);

        var updatedTicket = await _ticketRepository.UpdateAsync(ticket);

        // 履歴記録
        await _historyRepository.AddHistoryAsync(
            ticketId, 
            updatedBy, 
            "Status", 
            oldStatus.ToString(), 
            newStatus.ToString(), 
            HistoryActionType.StatusChanged);

        // 担当者に通知
        var assignments = await _assignmentRepository.GetAssignmentsByTicketIdAsync(ticketId);
        foreach (var assignment in assignments)
        {
            await _notificationService.CreateNotificationAsync(
                assignment.AssigneeId, 
                "Ticket Status Changed", 
                $"Ticket '{ticket.Title}' status changed to {newStatus}", 
                NotificationType.StatusChanged, 
                ticketId);
        }

        return updatedTicket;
    }

    public async Task<Ticket> AssignTicketAsync(Guid ticketId, string assigneeId, string assignedBy)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            throw new ArgumentException($"Ticket with ID {ticketId} not found.", nameof(ticketId));
        }

        // 既に割り当てられているかチェック
        var existingAssignment = await _assignmentRepository.GetActiveAssignmentAsync(ticketId, assigneeId);
        if (existingAssignment != null)
        {
            throw new InvalidOperationException($"Ticket is already assigned to user {assigneeId}");
        }

        var assignment = new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AssigneeId = assigneeId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };

        await _assignmentRepository.AddAsync(assignment);

        // 履歴記録
        await _historyRepository.AddHistoryAsync(
            ticketId, 
            assignedBy, 
            "Assignment", 
            null, 
            assigneeId, 
            HistoryActionType.Assigned);

        // 担当者に通知
        await _notificationService.CreateNotificationAsync(
            assigneeId, 
            "Ticket Assigned", 
            $"You have been assigned to ticket '{ticket.Title}'", 
            NotificationType.TicketAssigned, 
            ticketId);

        return ticket;
    }

    public async Task RemoveTicketAssignmentAsync(Guid ticketId, string assigneeId, string removedBy)
    {
        await _assignmentRepository.RemoveAssignmentAsync(ticketId, assigneeId);

        // 履歴記録
        await _historyRepository.AddHistoryAsync(
            ticketId, 
            removedBy, 
            "Assignment", 
            assigneeId, 
            null, 
            HistoryActionType.Unassigned);
    }

    public async Task<Comment> AddCommentAsync(Guid ticketId, string content, string authorId)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));
        
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("AuthorId cannot be empty", nameof(authorId));

        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            throw new ArgumentException($"Ticket with ID {ticketId} not found.", nameof(ticketId));
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Content = content,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow,
            IsEdited = false
        };

        var createdComment = await _commentRepository.AddAsync(comment);

        // チケットの最終更新日時を更新
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = authorId;
        await _ticketRepository.UpdateAsync(ticket);

        // 履歴記録
        await _historyRepository.AddHistoryAsync(
            ticketId, 
            authorId, 
            "Comment", 
            null, 
            "Comment added", 
            HistoryActionType.CommentAdded);

        // 担当者に通知（コメント作成者以外）
        var assignments = await _assignmentRepository.GetAssignmentsByTicketIdAsync(ticketId);
        foreach (var assignment in assignments.Where(a => a.AssigneeId != authorId))
        {
            await _notificationService.CreateNotificationAsync(
                assignment.AssigneeId, 
                "New Comment", 
                $"New comment added to ticket '{ticket.Title}'", 
                NotificationType.CommentAdded, 
                ticketId);
        }

        return createdComment;
    }

    public async Task<Comment> UpdateCommentAsync(Guid commentId, string content, string authorId)
    {
        var comment = await _commentRepository.GetByIdAsync(commentId);
        if (comment == null)
        {
            throw new ArgumentException($"Comment with ID {commentId} not found.", nameof(commentId));
        }

        if (comment.AuthorId != authorId)
        {
            throw new UnauthorizedAccessException("Only the comment author can edit this comment.");
        }

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;
        comment.IsEdited = true;

        return await _commentRepository.UpdateAsync(comment);
    }

    public async Task DeleteCommentAsync(Guid commentId, string authorId)
    {
        var comment = await _commentRepository.GetByIdAsync(commentId);
        if (comment == null)
        {
            throw new ArgumentException($"Comment with ID {commentId} not found.", nameof(commentId));
        }

        if (comment.AuthorId != authorId)
        {
            throw new UnauthorizedAccessException("Only the comment author can delete this comment.");
        }

        await _commentRepository.DeleteAsync(commentId);
    }

    public async Task<Ticket?> GetTicketAsync(Guid ticketId)
    {
        return await _ticketRepository.GetByIdAsync(ticketId);
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByProjectAsync(Guid projectId)
    {
        return await _ticketRepository.GetTicketsByProjectIdAsync(projectId);
    }

    public async Task<IEnumerable<Ticket>> GetTicketsByAssigneeAsync(string assigneeId)
    {
        return await _ticketRepository.GetTicketsByAssigneeAsync(assigneeId);
    }

    public async Task<PagedResult<Ticket>> SearchTicketsAsync(
        Guid projectId, 
        TicketSearchCriteria criteria, 
        int page = 1, 
        int pageSize = 20)
    {
        return await _ticketRepository.SearchTicketsAsync(projectId, criteria, page, pageSize);
    }

    public async Task<IEnumerable<Ticket>> GetRecentTicketsAsync(string userId, int count = 10)
    {
        return await _ticketRepository.GetRecentTicketsAsync(userId, count);
    }

    public async Task DeleteTicketAsync(Guid ticketId, string deletedBy)
    {
        await _ticketRepository.DeleteAsync(ticketId);
    }

    public async Task<bool> CanUserAccessTicketAsync(Guid ticketId, string userId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket == null) return false;

        return await _projectRepository.IsUserMemberOfProjectAsync(ticket.ProjectId, userId);
    }
}