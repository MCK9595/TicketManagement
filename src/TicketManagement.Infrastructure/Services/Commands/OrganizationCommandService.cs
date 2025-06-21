using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TicketManagement.Contracts.Commands;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services.Commands;

public class OrganizationCommandService : IOrganizationCommandService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _memberRepository;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<OrganizationCommandService> _logger;

    public OrganizationCommandService(
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository memberRepository,
        INotificationService notificationService,
        ICacheService cacheService,
        IHttpContextAccessor httpContextAccessor,
        IUserManagementService userManagementService,
        ILogger<OrganizationCommandService> logger)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _httpContextAccessor = httpContextAccessor;
        _userManagementService = userManagementService;
        _logger = logger;
    }

    public async Task<Guid> CreateOrganizationAsync(CreateOrganizationCommand command)
    {
        _logger.LogDebug("Creating organization: {Name} for user {UserId}", command.Name, command.CreatedBy);

        // Validate input
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Organization name cannot be empty", nameof(command.Name));

        if (string.IsNullOrWhiteSpace(command.CreatedBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(command.CreatedBy));

        // Check if organization name already exists
        var existingOrg = await _organizationRepository.GetByNameAsync(command.Name);
        if (existingOrg != null)
        {
            _logger.LogWarning("Attempt to create organization with duplicate name: {Name}", command.Name);
            throw new InvalidOperationException($"Organization with name '{command.Name}' already exists");
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            DisplayName = command.DisplayName ?? command.Name,
            Description = command.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.CreatedBy,
            IsActive = true
        };

        var createdOrg = await _organizationRepository.AddAsync(organization);
        _logger.LogDebug("Organization entity created with ID: {OrganizationId}", createdOrg.Id);

        // Add creator as admin with proper user context
        var adminMember = await CreateAdminMemberAsync(createdOrg.Id, command.CreatedBy);
        await _memberRepository.AddAsync(adminMember);

        // Clear user's organization cache
        await InvalidateUserCacheAsync(command.CreatedBy);

        _logger.LogInformation("Organization '{Name}' created successfully by user {UserId}", 
            command.Name, command.CreatedBy);

        return createdOrg.Id;
    }

    public async Task UpdateOrganizationAsync(UpdateOrganizationCommand command)
    {
        _logger.LogDebug("Updating organization: {OrganizationId}", command.OrganizationId);

        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);
        if (organization == null)
        {
            _logger.LogWarning("Attempt to update non-existent organization: {OrganizationId}", command.OrganizationId);
            throw new ArgumentException($"Organization with ID {command.OrganizationId} not found");
        }

        // Check authorization
        if (!await IsUserAdminAsync(command.OrganizationId, command.UpdatedBy))
        {
            _logger.LogWarning("Unauthorized attempt to update organization {OrganizationId} by user {UserId}", 
                command.OrganizationId, command.UpdatedBy);
            throw new UnauthorizedAccessException("User does not have permission to update this organization");
        }

        // Check for name conflicts
        if (organization.Name != command.Name)
        {
            var existingOrg = await _organizationRepository.GetByNameAsync(command.Name);
            if (existingOrg != null && existingOrg.Id != command.OrganizationId)
            {
                _logger.LogWarning("Attempt to update organization to duplicate name: {Name}", command.Name);
                throw new InvalidOperationException($"Organization with name '{command.Name}' already exists");
            }
        }

        // Update organization
        organization.Name = command.Name;
        organization.DisplayName = command.DisplayName ?? command.Name;
        organization.Description = command.Description;
        organization.UpdatedAt = DateTime.UtcNow;
        organization.UpdatedBy = command.UpdatedBy;

        await _organizationRepository.UpdateAsync(organization);

        // Clear caches
        await InvalidateOrganizationCacheAsync(command.OrganizationId);

        _logger.LogInformation("Organization '{Name}' updated by user {UserId}", 
            command.Name, command.UpdatedBy);
    }

    public async Task DeleteOrganizationAsync(DeleteOrganizationCommand command)
    {
        _logger.LogDebug("Deleting organization: {OrganizationId}", command.OrganizationId);

        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);
        if (organization == null)
        {
            _logger.LogWarning("Attempt to delete non-existent organization: {OrganizationId}", command.OrganizationId);
            throw new ArgumentException($"Organization with ID {command.OrganizationId} not found");
        }

        // Check authorization
        if (!await IsUserAdminAsync(command.OrganizationId, command.DeletedBy))
        {
            _logger.LogWarning("Unauthorized attempt to delete organization {OrganizationId} by user {UserId}", 
                command.OrganizationId, command.DeletedBy);
            throw new UnauthorizedAccessException("Only organization admins can delete the organization");
        }

        // Notify all members before deletion
        var members = await _memberRepository.GetActiveOrganizationMembersAsync(command.OrganizationId);
        var notificationTasks = members
            .Where(m => m.UserId != command.DeletedBy)
            .Select(m => _notificationService.CreateNotificationAsync(
                m.UserId,
                "Organization Deleted",
                $"Organization '{organization.Name}' has been deleted",
                NotificationType.OrganizationDeleted));

        await Task.WhenAll(notificationTasks);

        // Delete organization
        await _organizationRepository.DeleteAsync(command.OrganizationId);

        // Clear all related caches
        await InvalidateOrganizationCacheAsync(command.OrganizationId);
        foreach (var member in members)
        {
            await InvalidateUserCacheAsync(member.UserId);
        }

        _logger.LogInformation("Organization '{Name}' deleted by user {UserId}", 
            organization.Name, command.DeletedBy);
    }

    public async Task<Guid> AddMemberAsync(AddOrganizationMemberCommand command)
    {
        _logger.LogDebug("Adding member to organization: {OrganizationId}, User: {UserId}", 
            command.OrganizationId, command.UserId);

        // Check authorization
        if (!await IsUserAdminAsync(command.OrganizationId, command.InvitedBy))
        {
            _logger.LogWarning("Unauthorized attempt to add member to organization {OrganizationId} by user {UserId}", 
                command.OrganizationId, command.InvitedBy);
            throw new UnauthorizedAccessException("User does not have permission to add members");
        }

        // Check member limit
        if (!await CanAddMemberAsync(command.OrganizationId))
        {
            _logger.LogWarning("Attempt to add member to organization {OrganizationId} that has reached member limit", 
                command.OrganizationId);
            throw new InvalidOperationException("Organization has reached its member limit");
        }

        // Check if member already exists
        var existingMember = await _memberRepository.GetMemberAsync(command.OrganizationId, command.UserId);
        if (existingMember != null)
        {
            if (existingMember.IsActive)
            {
                _logger.LogDebug("User {UserId} is already an active member of organization {OrganizationId}", 
                    command.UserId, command.OrganizationId);
                throw new InvalidOperationException("User is already a member of this organization");
            }

            // Reactivate inactive member
            existingMember.IsActive = true;
            existingMember.Role = command.Role;
            existingMember.JoinedAt = DateTime.UtcNow;
            existingMember.InvitedBy = command.InvitedBy;

            await _memberRepository.UpdateAsync(existingMember);
            _logger.LogDebug("Reactivated member {UserId} in organization {OrganizationId}", 
                command.UserId, command.OrganizationId);
            
            return existingMember.Id;
        }

        // Create new member
        // Get user information if not provided
        string userName = command.UserName;
        string userEmail = command.UserEmail;

        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userEmail))
        {
            try
            {
                var userInfo = await _userManagementService.GetUserByIdAsync(command.UserId);
                if (userInfo != null)
                {
                    userName = string.IsNullOrEmpty(userName) ? (userInfo.DisplayName ?? userInfo.Username ?? command.UserId) : userName;
                    userEmail = string.IsNullOrEmpty(userEmail) ? (userInfo.Email ?? string.Empty) : userEmail;
                    _logger.LogDebug("Retrieved missing user info from Keycloak for user {UserId}", command.UserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve user info from Keycloak for user {UserId}", command.UserId);
            }
        }

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = command.OrganizationId,
            UserId = command.UserId,
            UserName = userName,
            UserEmail = userEmail,
            Role = command.Role,
            JoinedAt = DateTime.UtcNow,
            InvitedBy = command.InvitedBy,
            IsActive = true
        };

        var addedMember = await _memberRepository.AddAsync(member);

        // Send notification
        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);
        await _notificationService.CreateNotificationAsync(
            command.UserId,
            "Added to Organization",
            $"You have been added to organization '{organization?.Name}' as {command.Role}",
            NotificationType.OrganizationMember);

        // Clear caches
        await InvalidateUserCacheAsync(command.UserId);

        _logger.LogInformation("User {UserId} added to organization {OrganizationId} with role {Role}", 
            command.UserId, command.OrganizationId, command.Role);

        return addedMember.Id;
    }

    public async Task UpdateMemberRoleAsync(UpdateMemberRoleCommand command)
    {
        _logger.LogDebug("Updating member role in organization: {OrganizationId}, User: {UserId}, NewRole: {Role}", 
            command.OrganizationId, command.UserId, command.NewRole);

        // Check authorization
        if (!await IsUserAdminAsync(command.OrganizationId, command.UpdatedBy))
        {
            _logger.LogWarning("Unauthorized attempt to update member role in organization {OrganizationId} by user {UserId}", 
                command.OrganizationId, command.UpdatedBy);
            throw new UnauthorizedAccessException("User does not have permission to update member roles");
        }

        var member = await _memberRepository.GetMemberAsync(command.OrganizationId, command.UserId);
        if (member == null || !member.IsActive)
        {
            _logger.LogWarning("Attempt to update role for non-existent member: {UserId} in organization {OrganizationId}", 
                command.UserId, command.OrganizationId);
            throw new ArgumentException("Member not found in organization");
        }

        // Prevent last admin from being demoted
        if (member.Role == OrganizationRole.Admin && command.NewRole != OrganizationRole.Admin)
        {
            var adminCount = (await _memberRepository.GetOrganizationAdminsAsync(command.OrganizationId)).Count();
            if (adminCount <= 1)
            {
                _logger.LogWarning("Attempt to demote last admin in organization {OrganizationId}", command.OrganizationId);
                throw new InvalidOperationException("Cannot demote the last admin of the organization");
            }
        }

        var oldRole = member.Role;
        member.Role = command.NewRole;
        await _memberRepository.UpdateAsync(member);

        // Send notification
        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);
        await _notificationService.CreateNotificationAsync(
            command.UserId,
            "Role Updated",
            $"Your role in organization '{organization?.Name}' has been changed from {oldRole} to {command.NewRole}",
            NotificationType.StatusChanged);

        _logger.LogInformation("Member {UserId} role updated from {OldRole} to {NewRole} in organization {OrganizationId}", 
            command.UserId, oldRole, command.NewRole, command.OrganizationId);
    }

    public async Task RemoveMemberAsync(RemoveMemberCommand command)
    {
        _logger.LogDebug("Removing member from organization: {OrganizationId}, User: {UserId}", 
            command.OrganizationId, command.UserId);

        // Check authorization
        if (!await IsUserAdminAsync(command.OrganizationId, command.RemovedBy))
        {
            _logger.LogWarning("Unauthorized attempt to remove member from organization {OrganizationId} by user {UserId}", 
                command.OrganizationId, command.RemovedBy);
            throw new UnauthorizedAccessException("User does not have permission to remove members");
        }

        var member = await _memberRepository.GetMemberAsync(command.OrganizationId, command.UserId);
        if (member == null || !member.IsActive)
        {
            _logger.LogWarning("Attempt to remove non-existent member: {UserId} from organization {OrganizationId}", 
                command.UserId, command.OrganizationId);
            throw new ArgumentException("Member not found in organization");
        }

        // Prevent last admin from being removed
        if (member.Role == OrganizationRole.Admin)
        {
            var adminCount = (await _memberRepository.GetOrganizationAdminsAsync(command.OrganizationId)).Count();
            if (adminCount <= 1)
            {
                _logger.LogWarning("Attempt to remove last admin from organization {OrganizationId}", command.OrganizationId);
                throw new InvalidOperationException("Cannot remove the last admin of the organization");
            }
        }

        await _memberRepository.RemoveMemberAsync(command.OrganizationId, command.UserId);

        // Send notification
        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);
        await _notificationService.CreateNotificationAsync(
            command.UserId,
            "Removed from Organization",
            $"You have been removed from organization '{organization?.Name}'",
            NotificationType.OrganizationMember);

        // Clear caches
        await InvalidateUserCacheAsync(command.UserId);

        _logger.LogInformation("Member {UserId} removed from organization {OrganizationId}", 
            command.UserId, command.OrganizationId);
    }

    #region Private Helper Methods

    private async Task<OrganizationMember> CreateAdminMemberAsync(Guid organizationId, string userId)
    {
        _logger.LogTrace("Creating admin member for organization {OrganizationId}, user {UserId}", organizationId, userId);

        // Get user information from Keycloak via UserManagementService
        string userName = userId;
        string userEmail = string.Empty;

        try
        {
            var userInfo = await _userManagementService.GetUserByIdAsync(userId);
            if (userInfo != null)
            {
                userName = userInfo.DisplayName ?? userInfo.Username ?? userId;
                userEmail = userInfo.Email ?? string.Empty;
                _logger.LogDebug("Retrieved user info from Keycloak: UserName={UserName}, Email={Email}", userName, userEmail);
            }
            else
            {
                _logger.LogWarning("Could not retrieve user info from Keycloak for user {UserId}, using fallback values", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve user info from Keycloak for user {UserId}, using fallback values", userId);
        }

        return new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            UserName = userName,
            UserEmail = userEmail,
            Role = OrganizationRole.Admin,
            JoinedAt = DateTime.UtcNow,
            InvitedBy = "system",
            IsActive = true
        };
    }

    private async Task<bool> IsUserAdminAsync(Guid organizationId, string userId)
    {
        return await _organizationRepository.IsUserAdminOfOrganizationAsync(organizationId, userId);
    }

    private async Task<bool> CanAddMemberAsync(Guid organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null) return false;

        var currentCount = await _organizationRepository.GetMemberCountAsync(organizationId);
        return currentCount < organization.MaxMembers;
    }

    private async Task InvalidateOrganizationCacheAsync(Guid organizationId)
    {
        await _cacheService.RemoveAsync($"org:{organizationId}");
    }

    private async Task InvalidateUserCacheAsync(string userId)
    {
        await _cacheService.RemoveAsync($"user-orgs:{userId}");
    }

    #endregion
}