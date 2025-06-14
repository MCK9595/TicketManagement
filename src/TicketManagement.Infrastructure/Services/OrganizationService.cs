using Microsoft.Extensions.Logging;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _memberRepository;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository memberRepository,
        INotificationService notificationService,
        ICacheService cacheService,
        ILogger<OrganizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Organization> CreateOrganizationAsync(string name, string? displayName, string? description, string createdBy)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        // Check if organization name already exists
        var existingOrg = await _organizationRepository.GetByNameAsync(name);
        if (existingOrg != null)
            throw new InvalidOperationException($"Organization with name '{name}' already exists");

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = displayName ?? name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsActive = true
        };

        var createdOrg = await _organizationRepository.AddAsync(organization);

        // Add creator as admin
        var adminMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = createdOrg.Id,
            UserId = createdBy,
            UserName = createdBy, // TODO: Get actual user name from user service
            Role = OrganizationRole.Admin,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _memberRepository.AddAsync(adminMember);

        _logger.LogInformation("Organization created: {OrganizationId} by user {UserId}. Added admin member with UserId: {AdminUserId}", 
            createdOrg.Id, createdBy, adminMember.UserId);

        return createdOrg;
    }

    public async Task<Organization> UpdateOrganizationAsync(Guid organizationId, string name, string? displayName, string? description, string updatedBy)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
            throw new ArgumentException($"Organization with ID {organizationId} not found", nameof(organizationId));

        // Check if user can manage organization
        if (!await CanUserManageOrganizationAsync(organizationId, updatedBy))
            throw new UnauthorizedAccessException("User does not have permission to update this organization");

        // Check if new name conflicts with existing organization
        if (organization.Name != name)
        {
            var existingOrg = await _organizationRepository.GetByNameAsync(name);
            if (existingOrg != null && existingOrg.Id != organizationId)
                throw new InvalidOperationException($"Organization with name '{name}' already exists");
        }

        organization.Name = name;
        organization.DisplayName = displayName ?? name;
        organization.Description = description;
        organization.UpdatedAt = DateTime.UtcNow;
        organization.UpdatedBy = updatedBy;

        var updatedOrg = await _organizationRepository.UpdateAsync(organization);

        // Clear cache
        await _cacheService.RemoveAsync($"org:{organizationId}");

        _logger.LogInformation("Organization updated: {OrganizationId} by user {UserId}", organizationId, updatedBy);

        return updatedOrg;
    }

    public async Task DeleteOrganizationAsync(Guid organizationId, string deletedBy)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
            throw new ArgumentException($"Organization with ID {organizationId} not found", nameof(organizationId));

        // Check if user is admin
        if (!await IsUserAdminOfOrganizationAsync(organizationId, deletedBy))
            throw new UnauthorizedAccessException("Only organization admins can delete the organization");

        // Send notifications to all members
        var members = await _memberRepository.GetActiveOrganizationMembersAsync(organizationId);
        foreach (var member in members)
        {
            if (member.UserId != deletedBy)
            {
                await _notificationService.CreateNotificationAsync(
                    member.UserId,
                    "Organization Deleted",
                    $"Organization '{organization.Name}' has been deleted",
                    NotificationType.OrganizationDeleted);
            }
        }

        // Delete organization (cascade deletes projects, members, etc.)
        await _organizationRepository.DeleteAsync(organizationId);

        // Clear caches
        await _cacheService.RemoveAsync($"org:{organizationId}");
        foreach (var member in members)
        {
            await _cacheService.RemoveAsync($"user-orgs:{member.UserId}");
        }

        _logger.LogInformation("Organization deleted: {OrganizationId} by user {UserId}", organizationId, deletedBy);
    }

    public async Task<Organization?> GetOrganizationAsync(Guid organizationId)
    {
        var cacheKey = $"org:{organizationId}";
        var cached = await _cacheService.GetAsync<Organization>(cacheKey);
        if (cached != null) return cached;

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization != null)
        {
            await _cacheService.SetAsync(cacheKey, organization, TimeSpan.FromMinutes(10));
        }

        return organization;
    }

    public async Task<Organization?> GetOrganizationWithDetailsAsync(Guid organizationId)
    {
        return await _organizationRepository.GetByIdWithMembersAsync(organizationId);
    }

    public async Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId)
    {
        var cacheKey = $"user-orgs:{userId}";
        var cached = await _cacheService.GetAsync<List<Organization>>(cacheKey);
        if (cached != null) return cached;

        var organizations = await _organizationRepository.GetUserOrganizationsAsync(userId);
        var orgList = organizations.ToList();

        await _cacheService.SetAsync(cacheKey, orgList, TimeSpan.FromMinutes(5));

        return orgList;
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersForUserAsync(string userId)
    {
        _logger.LogInformation("Getting organization memberships for user: {UserId}", userId);
        var memberships = await _memberRepository.GetUserOrganizationMembershipsAsync(userId);
        _logger.LogInformation("Found {Count} organization memberships for user {UserId}", memberships.Count(), userId);
        return memberships;
    }

    public async Task<OrganizationMember> AddMemberAsync(Guid organizationId, string userId, string userName, string? userEmail, OrganizationRole role, string invitedBy)
    {
        // Check if user can manage members
        if (!await CanUserManageMembersAsync(organizationId, invitedBy))
            throw new UnauthorizedAccessException("User does not have permission to add members");

        // Check member limit
        if (!await CanAddMemberAsync(organizationId))
            throw new InvalidOperationException("Organization has reached its member limit");

        // Check if member already exists
        var existingMember = await _memberRepository.GetMemberAsync(organizationId, userId);
        if (existingMember != null)
        {
            if (existingMember.IsActive)
                throw new InvalidOperationException("User is already a member of this organization");

            // Reactivate inactive member
            existingMember.IsActive = true;
            existingMember.Role = role;
            existingMember.JoinedAt = DateTime.UtcNow;
            existingMember.InvitedBy = invitedBy;

            await _memberRepository.UpdateAsync(existingMember);
            return existingMember;
        }

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            UserName = userName,
            UserEmail = userEmail,
            Role = role,
            JoinedAt = DateTime.UtcNow,
            InvitedBy = invitedBy,
            IsActive = true
        };

        var addedMember = await _memberRepository.AddAsync(member);

        // Send notification
        var organization = await GetOrganizationAsync(organizationId);
        await _notificationService.CreateNotificationAsync(
            userId,
            "Added to Organization",
            $"You have been added to organization '{organization?.Name}' as {role}",
            NotificationType.OrganizationMember);

        // Clear cache
        await _cacheService.RemoveAsync($"user-orgs:{userId}");

        _logger.LogInformation("Member added to organization: {OrganizationId}, User: {UserId}, Role: {Role}", 
            organizationId, userId, role);

        return addedMember;
    }

    public async Task<OrganizationMember> UpdateMemberRoleAsync(Guid organizationId, string userId, OrganizationRole newRole, string updatedBy)
    {
        // Check if user can manage members
        if (!await CanUserManageMembersAsync(organizationId, updatedBy))
            throw new UnauthorizedAccessException("User does not have permission to update member roles");

        var member = await _memberRepository.GetMemberAsync(organizationId, userId);
        if (member == null || !member.IsActive)
            throw new ArgumentException("Member not found in organization");

        // Prevent last admin from being demoted
        if (member.Role == OrganizationRole.Admin && newRole != OrganizationRole.Admin)
        {
            var adminCount = (await _memberRepository.GetOrganizationAdminsAsync(organizationId)).Count();
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot demote the last admin of the organization");
        }

        var oldRole = member.Role;
        member.Role = newRole;
        await _memberRepository.UpdateAsync(member);

        // Send notification
        var organization = await GetOrganizationAsync(organizationId);
        await _notificationService.CreateNotificationAsync(
            userId,
            "Role Updated",
            $"Your role in organization '{organization?.Name}' has been changed from {oldRole} to {newRole}",
            NotificationType.StatusChanged);

        _logger.LogInformation("Member role updated: Organization: {OrganizationId}, User: {UserId}, OldRole: {OldRole}, NewRole: {NewRole}", 
            organizationId, userId, oldRole, newRole);

        return member;
    }

    public async Task RemoveMemberAsync(Guid organizationId, string userId, string removedBy)
    {
        // Check if user can manage members
        if (!await CanUserManageMembersAsync(organizationId, removedBy))
            throw new UnauthorizedAccessException("User does not have permission to remove members");

        var member = await _memberRepository.GetMemberAsync(organizationId, userId);
        if (member == null || !member.IsActive)
            throw new ArgumentException("Member not found in organization");

        // Prevent last admin from being removed
        if (member.Role == OrganizationRole.Admin)
        {
            var adminCount = (await _memberRepository.GetOrganizationAdminsAsync(organizationId)).Count();
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot remove the last admin of the organization");
        }

        await _memberRepository.RemoveMemberAsync(organizationId, userId);

        // Send notification
        var organization = await GetOrganizationAsync(organizationId);
        await _notificationService.CreateNotificationAsync(
            userId,
            "Removed from Organization",
            $"You have been removed from organization '{organization?.Name}'",
            NotificationType.OrganizationMember);

        // Clear cache
        await _cacheService.RemoveAsync($"user-orgs:{userId}");

        _logger.LogInformation("Member removed from organization: {OrganizationId}, User: {UserId}", organizationId, userId);
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId)
    {
        return await _memberRepository.GetActiveOrganizationMembersAsync(organizationId);
    }

    public async Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId)
    {
        var member = await _memberRepository.GetMemberAsync(organizationId, userId);
        return member?.IsActive == true ? member : null;
    }

    public async Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, string userId)
    {
        return await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId) != null;
    }

    public async Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId)
    {
        return await IsUserAdminOfOrganizationAsync(organizationId, userId);
    }

    public async Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId)
    {
        var role = await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId);
        return role == OrganizationRole.Manager || role == OrganizationRole.Admin;
    }

    public async Task<bool> CanUserManageMembersAsync(Guid organizationId, string userId)
    {
        return await IsUserAdminOfOrganizationAsync(organizationId, userId);
    }

    public async Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId)
    {
        return await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId);
    }

    public async Task<bool> CanCreateProjectAsync(Guid organizationId)
    {
        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null) return false;

        var currentCount = await _organizationRepository.GetProjectCountAsync(organizationId);
        return currentCount < organization.MaxProjects;
    }

    public async Task<bool> CanAddMemberAsync(Guid organizationId)
    {
        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null) return false;

        var currentCount = await _organizationRepository.GetMemberCountAsync(organizationId);
        return currentCount < organization.MaxMembers;
    }

    public async Task<(int current, int max)> GetProjectLimitsAsync(Guid organizationId)
    {
        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null) throw new ArgumentException("Organization not found");

        var current = await _organizationRepository.GetProjectCountAsync(organizationId);
        return (current, organization.MaxProjects);
    }

    public async Task<(int current, int max)> GetMemberLimitsAsync(Guid organizationId)
    {
        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null) throw new ArgumentException("Organization not found");

        var current = await _organizationRepository.GetMemberCountAsync(organizationId);
        return (current, organization.MaxMembers);
    }

    private async Task<bool> IsUserAdminOfOrganizationAsync(Guid organizationId, string userId)
    {
        return await _organizationRepository.IsUserAdminOfOrganizationAsync(organizationId, userId);
    }
}