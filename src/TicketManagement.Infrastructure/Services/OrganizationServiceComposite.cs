using TicketManagement.Contracts.Commands;
using TicketManagement.Contracts.Queries;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services;

/// <summary>
/// Composite service that implements the original IOrganizationService interface
/// by delegating to the new CQRS-based services. This allows for gradual migration.
/// </summary>
public class OrganizationServiceComposite : IOrganizationService
{
    private readonly IOrganizationCommandService _commandService;
    private readonly IOrganizationQueryService _queryService;
    private readonly IOrganizationAuthorizationService _authorizationService;

    public OrganizationServiceComposite(
        IOrganizationCommandService commandService,
        IOrganizationQueryService queryService,
        IOrganizationAuthorizationService authorizationService)
    {
        _commandService = commandService;
        _queryService = queryService;
        _authorizationService = authorizationService;
    }

    #region Command Operations

    public async Task<Organization> CreateOrganizationAsync(string name, string? displayName, string? description, string createdBy)
    {
        var command = new CreateOrganizationCommand(name, displayName, description, createdBy);
        var organizationId = await _commandService.CreateOrganizationAsync(command);
        
        // Return the created organization
        var organization = await _queryService.GetOrganizationAsync(organizationId);
        return organization ?? throw new InvalidOperationException("Failed to retrieve created organization");
    }

    public async Task<Organization> UpdateOrganizationAsync(Guid organizationId, string name, string? displayName, string? description, string updatedBy)
    {
        var command = new UpdateOrganizationCommand(organizationId, name, displayName, description, updatedBy);
        await _commandService.UpdateOrganizationAsync(command);
        
        // Return the updated organization
        var organization = await _queryService.GetOrganizationAsync(organizationId);
        return organization ?? throw new InvalidOperationException("Failed to retrieve updated organization");
    }

    public async Task DeleteOrganizationAsync(Guid organizationId, string deletedBy)
    {
        var command = new DeleteOrganizationCommand(organizationId, deletedBy);
        await _commandService.DeleteOrganizationAsync(command);
    }

    public async Task<OrganizationMember> AddMemberAsync(Guid organizationId, string userId, string userName, string? userEmail, OrganizationRole role, string invitedBy)
    {
        var command = new AddOrganizationMemberCommand(organizationId, userId, userName, userEmail, role, invitedBy);
        var memberId = await _commandService.AddMemberAsync(command);
        
        // Return the created member - need to get it from the repository
        var member = await _queryService.GetMemberAsync(organizationId, userId);
        return member ?? throw new InvalidOperationException("Failed to retrieve added member");
    }

    public async Task<OrganizationMember> UpdateMemberRoleAsync(Guid organizationId, string userId, OrganizationRole newRole, string updatedBy)
    {
        var command = new UpdateMemberRoleCommand(organizationId, userId, newRole, updatedBy);
        await _commandService.UpdateMemberRoleAsync(command);
        
        // Return the updated member
        var member = await _queryService.GetMemberAsync(organizationId, userId);
        return member ?? throw new InvalidOperationException("Failed to retrieve updated member");
    }

    public async Task RemoveMemberAsync(Guid organizationId, string userId, string removedBy)
    {
        var command = new RemoveMemberCommand(organizationId, userId, removedBy);
        await _commandService.RemoveMemberAsync(command);
    }

    #endregion

    #region Query Operations

    public async Task<Organization?> GetOrganizationAsync(Guid organizationId)
    {
        return await _queryService.GetOrganizationAsync(organizationId);
    }

    public async Task<Organization?> GetOrganizationWithDetailsAsync(Guid organizationId)
    {
        return await _queryService.GetOrganizationWithDetailsAsync(organizationId);
    }

    public async Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId)
    {
        return await _queryService.GetUserOrganizationsAsync(userId);
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId)
    {
        return await _queryService.GetOrganizationMembersAsync(organizationId);
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersForUserAsync(string userId)
    {
        return await _queryService.GetOrganizationMembersForUserAsync(userId);
    }

    public async Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId)
    {
        return await _queryService.GetMemberAsync(organizationId, userId);
    }

    public async Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId)
    {
        return await _queryService.GetUserRoleAsync(organizationId, userId);
    }

    public async Task<(int current, int max)> GetProjectLimitsAsync(Guid organizationId)
    {
        return await _queryService.GetProjectLimitsAsync(organizationId);
    }

    public async Task<(int current, int max)> GetMemberLimitsAsync(Guid organizationId)
    {
        return await _queryService.GetMemberLimitsAsync(organizationId);
    }

    #endregion

    #region Authorization Operations

    public async Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, string userId)
    {
        return await _authorizationService.CanUserAccessOrganizationAsync(organizationId, userId);
    }

    public async Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId)
    {
        return await _authorizationService.CanUserManageOrganizationAsync(organizationId, userId);
    }

    public async Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId)
    {
        return await _authorizationService.CanUserCreateProjectAsync(organizationId, userId);
    }

    public async Task<bool> CanUserManageMembersAsync(Guid organizationId, string userId)
    {
        return await _authorizationService.CanUserManageMembersAsync(organizationId, userId);
    }

    public async Task<bool> CanCreateProjectAsync(Guid organizationId)
    {
        return await _authorizationService.CanCreateProjectAsync(organizationId);
    }

    public async Task<bool> CanAddMemberAsync(Guid organizationId)
    {
        return await _authorizationService.CanAddMemberAsync(organizationId);
    }

    #endregion
}