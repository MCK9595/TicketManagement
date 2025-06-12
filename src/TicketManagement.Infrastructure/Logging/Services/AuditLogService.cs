using TicketManagement.Core.Entities;
using TicketManagement.Infrastructure.Logging.Models;

namespace TicketManagement.Infrastructure.Logging.Services;

/// <summary>
/// 監査ログサービスのインターフェース
/// </summary>
public interface IAuditLogService
{
    // プロジェクト関連の監査ログ
    Task LogProjectCreatedAsync(Project project, CancellationToken cancellationToken = default);
    Task LogProjectUpdatedAsync(Project oldProject, Project newProject, CancellationToken cancellationToken = default);
    Task LogProjectDeletedAsync(Project project, CancellationToken cancellationToken = default);
    Task LogProjectMemberAddedAsync(Guid projectId, ProjectMember member, CancellationToken cancellationToken = default);
    Task LogProjectMemberRemovedAsync(Guid projectId, ProjectMember member, CancellationToken cancellationToken = default);
    Task LogProjectMemberRoleChangedAsync(Guid projectId, string memberId, string oldRole, string newRole, CancellationToken cancellationToken = default);

    // チケット関連の監査ログ
    Task LogTicketCreatedAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task LogTicketUpdatedAsync(Ticket oldTicket, Ticket newTicket, CancellationToken cancellationToken = default);
    Task LogTicketDeletedAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task LogTicketStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus, CancellationToken cancellationToken = default);
    Task LogTicketAssignedAsync(Ticket ticket, string assigneeId, CancellationToken cancellationToken = default);
    Task LogTicketUnassignedAsync(Ticket ticket, string previousAssigneeId, CancellationToken cancellationToken = default);

    // コメント関連の監査ログ
    Task LogCommentCreatedAsync(Comment comment, CancellationToken cancellationToken = default);
    Task LogCommentUpdatedAsync(Comment oldComment, Comment newComment, CancellationToken cancellationToken = default);
    Task LogCommentDeletedAsync(Comment comment, CancellationToken cancellationToken = default);

    // 通知関連の監査ログ
    Task LogNotificationSentAsync(Notification notification, CancellationToken cancellationToken = default);
    Task LogNotificationReadAsync(Notification notification, CancellationToken cancellationToken = default);

    // データアクセス監査ログ
    Task LogDataAccessAsync(string operation, string entityType, string entityId, CancellationToken cancellationToken = default);
    Task LogDataExportAsync(string exportType, string[] entityIds, CancellationToken cancellationToken = default);
    Task LogBulkOperationAsync(string operation, string entityType, int affectedCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// 監査ログサービスの実装
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IStructuredLogger _structuredLogger;
    private readonly ILogEnrichmentService _enrichmentService;

    public AuditLogService(IStructuredLogger structuredLogger, ILogEnrichmentService enrichmentService)
    {
        _structuredLogger = structuredLogger;
        _enrichmentService = enrichmentService;
    }

    #region Project Audit Logs

    public async Task LogProjectCreatedAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_CREATE,
            "Project",
            project.Id.ToString(),
            newValue: new
            {
                project.Name,
                project.Description,
                project.CreatedBy,
                project.IsActive
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogProjectUpdatedAsync(Project oldProject, Project newProject, CancellationToken cancellationToken = default)
    {
        var changes = new Dictionary<string, object>();
        
        if (oldProject.Name != newProject.Name)
            changes["Name"] = new { Old = oldProject.Name, New = newProject.Name };
            
        if (oldProject.Description != newProject.Description)
            changes["Description"] = new { Old = oldProject.Description, New = newProject.Description };
            
        if (oldProject.IsActive != newProject.IsActive)
            changes["IsActive"] = new { Old = oldProject.IsActive, New = newProject.IsActive };

        if (changes.Count > 0)
        {
            await _structuredLogger.LogAuditEventAsync(
                _enrichmentService,
                LogEventTypes.DATA_UPDATE,
                "Project",
                newProject.Id.ToString(),
                oldProject,
                newProject,
                changes,
                cancellationToken);
        }
    }

    public async Task LogProjectDeletedAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_DELETE,
            "Project",
            project.Id.ToString(),
            oldValue: new
            {
                project.Name,
                project.Description,
                project.CreatedBy,
                project.IsActive
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogProjectMemberAddedAsync(Guid projectId, ProjectMember member, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            "ProjectMemberAdded",
            "ProjectMember",
            member.Id.ToString(),
            newValue: new
            {
                ProjectId = projectId,
                member.UserId,
                member.Role,
                member.JoinedAt
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogProjectMemberRemovedAsync(Guid projectId, ProjectMember member, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            "ProjectMemberRemoved",
            "ProjectMember",
            member.Id.ToString(),
            oldValue: new
            {
                ProjectId = projectId,
                member.UserId,
                member.Role,
                member.JoinedAt
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogProjectMemberRoleChangedAsync(Guid projectId, string memberId, string oldRole, string newRole, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.PERMISSION_CHANGE,
            "ProjectMember",
            memberId,
            changes: new Dictionary<string, object>
            {
                ["Role"] = new { Old = oldRole, New = newRole },
                ["ProjectId"] = projectId
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Ticket Audit Logs

    public async Task LogTicketCreatedAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_CREATE,
            "Ticket",
            ticket.Id.ToString(),
            newValue: new
            {
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.ProjectId,
                ticket.CreatedBy,
                ticket.Category,
                ticket.DueDate
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogTicketUpdatedAsync(Ticket oldTicket, Ticket newTicket, CancellationToken cancellationToken = default)
    {
        var changes = new Dictionary<string, object>();
        
        if (oldTicket.Title != newTicket.Title)
            changes["Title"] = new { Old = oldTicket.Title, New = newTicket.Title };
            
        if (oldTicket.Description != newTicket.Description)
            changes["Description"] = new { Old = oldTicket.Description, New = newTicket.Description };
            
        if (oldTicket.Status != newTicket.Status)
            changes["Status"] = new { Old = oldTicket.Status.ToString(), New = newTicket.Status.ToString() };
            
        if (oldTicket.Priority != newTicket.Priority)
            changes["Priority"] = new { Old = oldTicket.Priority.ToString(), New = newTicket.Priority.ToString() };
            
        if (oldTicket.Category != newTicket.Category)
            changes["Category"] = new { Old = oldTicket.Category, New = newTicket.Category };
            
        if (oldTicket.DueDate != newTicket.DueDate)
            changes["DueDate"] = new { Old = oldTicket.DueDate, New = newTicket.DueDate };

        if (changes.Count > 0)
        {
            await _structuredLogger.LogAuditEventAsync(
                _enrichmentService,
                LogEventTypes.DATA_UPDATE,
                "Ticket",
                newTicket.Id.ToString(),
                oldTicket,
                newTicket,
                changes,
                cancellationToken);
        }
    }

    public async Task LogTicketDeletedAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_DELETE,
            "Ticket",
            ticket.Id.ToString(),
            oldValue: new
            {
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.ProjectId,
                ticket.CreatedBy
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogTicketStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogBusinessEventAsync(
            _enrichmentService,
            "TicketManagement",
            LogEventTypes.TICKET_STATUS_CHANGED,
            "Ticket",
            ticket.Id.ToString(),
            true,
            context: new Dictionary<string, object>
            {
                ["OldStatus"] = oldStatus,
                ["NewStatus"] = newStatus,
                ["TicketTitle"] = ticket.Title,
                ["ProjectId"] = ticket.ProjectId
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogTicketAssignedAsync(Ticket ticket, string assigneeId, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogBusinessEventAsync(
            _enrichmentService,
            "TicketManagement",
            "TicketAssigned",
            "Ticket",
            ticket.Id.ToString(),
            true,
            context: new Dictionary<string, object>
            {
                ["AssigneeId"] = assigneeId,
                ["TicketTitle"] = ticket.Title,
                ["ProjectId"] = ticket.ProjectId
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogTicketUnassignedAsync(Ticket ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogBusinessEventAsync(
            _enrichmentService,
            "TicketManagement",
            "TicketUnassigned",
            "Ticket",
            ticket.Id.ToString(),
            true,
            context: new Dictionary<string, object>
            {
                ["PreviousAssigneeId"] = previousAssigneeId,
                ["TicketTitle"] = ticket.Title,
                ["ProjectId"] = ticket.ProjectId
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Comment Audit Logs

    public async Task LogCommentCreatedAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_CREATE,
            "Comment",
            comment.Id.ToString(),
            newValue: new
            {
                comment.Content,
                comment.TicketId,
                comment.AuthorId,
                comment.CreatedAt
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogCommentUpdatedAsync(Comment oldComment, Comment newComment, CancellationToken cancellationToken = default)
    {
        var changes = new Dictionary<string, object>();
        
        if (oldComment.Content != newComment.Content)
            changes["Content"] = new { Old = oldComment.Content, New = newComment.Content };

        if (changes.Count > 0)
        {
            await _structuredLogger.LogAuditEventAsync(
                _enrichmentService,
                LogEventTypes.DATA_UPDATE,
                "Comment",
                newComment.Id.ToString(),
                oldComment,
                newComment,
                changes,
                cancellationToken);
        }
    }

    public async Task LogCommentDeletedAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_DELETE,
            "Comment",
            comment.Id.ToString(),
            oldValue: new
            {
                comment.Content,
                comment.TicketId,
                comment.AuthorId,
                comment.CreatedAt
            },
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Notification Audit Logs

    public async Task LogNotificationSentAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogBusinessEventAsync(
            _enrichmentService,
            "NotificationSystem",
            LogEventTypes.NOTIFICATION_SENT,
            "Notification",
            notification.Id.ToString(),
            true,
            context: new Dictionary<string, object>
            {
                ["Type"] = notification.Type.ToString(),
                ["UserId"] = notification.UserId,
                ["Title"] = notification.Title
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogNotificationReadAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogUserActivityAsync(
            _enrichmentService,
            "NotificationRead",
            "Notification",
            notification.Id.ToString(),
            "User read notification",
            new Dictionary<string, object>
            {
                ["NotificationType"] = notification.Type.ToString(),
                ["NotificationTitle"] = notification.Title
            },
            cancellationToken);
    }

    #endregion

    #region Data Access Audit Logs

    public async Task LogDataAccessAsync(string operation, string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogUserActivityAsync(
            _enrichmentService,
            "DataAccess",
            entityType,
            entityId,
            $"User performed {operation} on {entityType}",
            new Dictionary<string, object>
            {
                ["Operation"] = operation
            },
            cancellationToken);
    }

    public async Task LogDataExportAsync(string exportType, string[] entityIds, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogSecurityEventAsync(
            _enrichmentService,
            LogEventTypes.DATA_EXPORT,
            exportType,
            "Export",
            true,
            metadata: new Dictionary<string, object>
            {
                ["ExportType"] = exportType,
                ["EntityCount"] = entityIds.Length,
                ["EntityIds"] = entityIds
            },
            cancellationToken: cancellationToken);
    }

    public async Task LogBulkOperationAsync(string operation, string entityType, int affectedCount, CancellationToken cancellationToken = default)
    {
        await _structuredLogger.LogAuditEventAsync(
            _enrichmentService,
            $"Bulk{operation}",
            entityType,
            changes: new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["AffectedCount"] = affectedCount
            },
            cancellationToken: cancellationToken);
    }

    #endregion
}