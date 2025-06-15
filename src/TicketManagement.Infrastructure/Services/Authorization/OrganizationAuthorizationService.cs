using Microsoft.Extensions.Logging;
using TicketManagement.Contracts.Queries;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services.Authorization;

public class OrganizationAuthorizationService : IOrganizationAuthorizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _memberRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrganizationAuthorizationService> _logger;

    // Cache keys for authorization results (shorter duration due to security sensitivity)
    private static class CacheKeys
    {
        public static string UserAccess(Guid orgId, string userId) => $"auth:access:{orgId}:{userId}";
        public static string UserRole(Guid orgId, string userId) => $"auth:role:{orgId}:{userId}";
    }

    private static readonly TimeSpan AuthorizationCacheDuration = TimeSpan.FromMinutes(2);

    public OrganizationAuthorizationService(
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository memberRepository,
        ICacheService cacheService,
        ILogger<OrganizationAuthorizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Access denied: null or empty userId for organization {OrganizationId}", organizationId);
            return false;
        }

        _logger.LogTrace("Checking user access: {UserId} to organization {OrganizationId}", userId, organizationId);

        var cacheKey = CacheKeys.UserAccess(organizationId, userId);
        var cached = await _cacheService.GetAsync<bool?>(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogTrace("User access result found in cache: {UserId} -> {HasAccess}", userId, cached.Value);
            return cached.Value;
        }

        var hasAccess = await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId) != null;
        
        // Cache the result for a short time to reduce repeated database calls
        await _cacheService.SetAsync(cacheKey, hasAccess, AuthorizationCacheDuration);

        if (!hasAccess)
        {
            _logger.LogDebug("Access denied: User {UserId} is not a member of organization {OrganizationId}", userId, organizationId);
        }

        return hasAccess;
    }

    public async Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Management access denied: null or empty userId for organization {OrganizationId}", organizationId);
            return false;
        }

        _logger.LogTrace("Checking user management access: {UserId} to organization {OrganizationId}", userId, organizationId);

        var canManage = await _organizationRepository.IsUserAdminOfOrganizationAsync(organizationId, userId);
        
        if (!canManage)
        {
            _logger.LogDebug("Management access denied: User {UserId} is not an admin of organization {OrganizationId}", 
                userId, organizationId);
        }

        return canManage;
    }

    public async Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Project creation denied: null or empty userId for organization {OrganizationId}", organizationId);
            return false;
        }

        _logger.LogTrace("Checking project creation permission: {UserId} in organization {OrganizationId}", userId, organizationId);

        var role = await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId);
        var canCreate = role == OrganizationRole.Manager || role == OrganizationRole.Admin;

        if (!canCreate)
        {
            _logger.LogDebug("Project creation denied: User {UserId} has insufficient role {Role} in organization {OrganizationId}", 
                userId, role, organizationId);
        }

        return canCreate;
    }

    public async Task<bool> CanUserManageMembersAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Member management denied: null or empty userId for organization {OrganizationId}", organizationId);
            return false;
        }

        _logger.LogTrace("Checking member management permission: {UserId} in organization {OrganizationId}", userId, organizationId);

        var canManage = await _organizationRepository.IsUserAdminOfOrganizationAsync(organizationId, userId);
        
        if (!canManage)
        {
            _logger.LogDebug("Member management denied: User {UserId} is not an admin of organization {OrganizationId}", 
                userId, organizationId);
        }

        return canManage;
    }

    public async Task<bool> CanCreateProjectAsync(Guid organizationId)
    {
        _logger.LogTrace("Checking project creation limits for organization: {OrganizationId}", organizationId);

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            _logger.LogWarning("Project creation denied: Organization {OrganizationId} not found", organizationId);
            return false;
        }

        var currentCount = await _organizationRepository.GetProjectCountAsync(organizationId);
        var canCreate = currentCount < organization.MaxProjects;

        if (!canCreate)
        {
            _logger.LogDebug("Project creation denied: Organization {OrganizationId} has reached project limit {Current}/{Max}", 
                organizationId, currentCount, organization.MaxProjects);
        }

        return canCreate;
    }

    public async Task<bool> CanAddMemberAsync(Guid organizationId)
    {
        _logger.LogTrace("Checking member addition limits for organization: {OrganizationId}", organizationId);

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            _logger.LogWarning("Member addition denied: Organization {OrganizationId} not found", organizationId);
            return false;
        }

        var currentCount = await _organizationRepository.GetMemberCountAsync(organizationId);
        var canAdd = currentCount < organization.MaxMembers;

        if (!canAdd)
        {
            _logger.LogDebug("Member addition denied: Organization {OrganizationId} has reached member limit {Current}/{Max}", 
                organizationId, currentCount, organization.MaxMembers);
        }

        return canAdd;
    }
}