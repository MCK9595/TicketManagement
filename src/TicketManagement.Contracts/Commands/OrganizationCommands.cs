using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.Commands;

// Command interfaces for organization operations
public interface IOrganizationCommandService
{
    Task<Guid> CreateOrganizationAsync(CreateOrganizationCommand command);
    Task UpdateOrganizationAsync(UpdateOrganizationCommand command);
    Task DeleteOrganizationAsync(DeleteOrganizationCommand command);
    Task<Guid> AddMemberAsync(AddOrganizationMemberCommand command);
    Task UpdateMemberRoleAsync(UpdateMemberRoleCommand command);
    Task RemoveMemberAsync(RemoveMemberCommand command);
}

// Command DTOs
public record CreateOrganizationCommand(
    string Name,
    string? DisplayName,
    string? Description,
    string CreatedBy
);

public record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string? DisplayName,
    string? Description,
    string UpdatedBy
);

public record DeleteOrganizationCommand(
    Guid OrganizationId,
    string DeletedBy
);

public record AddOrganizationMemberCommand(
    Guid OrganizationId,
    string UserId,
    string UserName,
    string? UserEmail,
    OrganizationRole Role,
    string InvitedBy
);

public record UpdateMemberRoleCommand(
    Guid OrganizationId,
    string UserId,
    OrganizationRole NewRole,
    string UpdatedBy
);

public record RemoveMemberCommand(
    Guid OrganizationId,
    string UserId,
    string RemovedBy
);