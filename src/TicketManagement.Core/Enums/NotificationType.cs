namespace TicketManagement.Core.Enums;

public enum NotificationType
{
    TicketAssigned = 0,
    CommentAdded = 1,
    StatusChanged = 2,
    MentionedInComment = 3,
    TicketDeleted = 4,
    ProjectDeleted = 5,
    ProjectMember = 6,
    OrganizationMember = 7,
    OrganizationDeleted = 8
}