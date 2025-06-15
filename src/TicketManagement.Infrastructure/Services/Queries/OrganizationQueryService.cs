using Microsoft.Extensions.Logging;
using TicketManagement.Contracts.Queries;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Infrastructure.Services.Queries;

public class OrganizationQueryService : IOrganizationQueryService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _memberRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrganizationQueryService> _logger;

    // Cache keys constants to avoid magic strings
    private static class CacheKeys
    {
        public static string Organization(Guid id) => $"org:{id}";
        public static string UserOrganizations(string userId) => $"user-orgs:{userId}";
        public static string OrganizationMembers(Guid orgId) => $"org-members:{orgId}";
        public static string UserMemberships(string userId) => $"user-memberships:{userId}";
    }

    // Cache durations
    private static readonly TimeSpan OrganizationCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UserOrganizationsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MembersCacheDuration = TimeSpan.FromMinutes(3);

    public OrganizationQueryService(
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository memberRepository,
        ICacheService cacheService,
        ILogger<OrganizationQueryService> logger)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Organization?> GetOrganizationAsync(Guid organizationId)
    {
        _logger.LogTrace("Getting organization: {OrganizationId}", organizationId);

        var cacheKey = CacheKeys.Organization(organizationId);
        var cached = await _cacheService.GetAsync<Organization>(cacheKey);
        if (cached != null)
        {
            _logger.LogTrace("Organization {OrganizationId} found in cache", organizationId);
            return cached;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization != null)
        {
            await _cacheService.SetAsync(cacheKey, organization, OrganizationCacheDuration);
            _logger.LogTrace("Organization {OrganizationId} cached for {Duration}", organizationId, OrganizationCacheDuration);
        }
        else
        {
            _logger.LogDebug("Organization not found: {OrganizationId}", organizationId);
        }

        return organization;
    }

    public async Task<Organization?> GetOrganizationWithDetailsAsync(Guid organizationId)
    {
        _logger.LogTrace("Getting organization with details: {OrganizationId}", organizationId);

        // Don't cache detailed views as they may be inconsistent
        var organization = await _organizationRepository.GetByIdWithMembersAsync(organizationId);
        
        if (organization == null)
        {
            _logger.LogDebug("Organization with details not found: {OrganizationId}", organizationId);
        }

        return organization;
    }

    public async Task<IEnumerable<Organization>> GetUserOrganizationsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetUserOrganizationsAsync called with null or empty userId");
            return Enumerable.Empty<Organization>();
        }

        _logger.LogTrace("Getting organizations for user: {UserId}", userId);

        var cacheKey = CacheKeys.UserOrganizations(userId);
        var cached = await _cacheService.GetAsync<List<Organization>>(cacheKey);
        if (cached != null)
        {
            _logger.LogTrace("User organizations for {UserId} found in cache", userId);
            return cached;
        }

        var organizations = await _organizationRepository.GetUserOrganizationsAsync(userId);
        var orgList = organizations.ToList();

        if (orgList.Any())
        {
            await _cacheService.SetAsync(cacheKey, orgList, UserOrganizationsCacheDuration);
            _logger.LogDebug("Found {Count} organizations for user {UserId}", orgList.Count, userId);
        }
        else
        {
            _logger.LogDebug("No organizations found for user {UserId}", userId);
        }

        return orgList;
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersAsync(Guid organizationId)
    {
        _logger.LogTrace("Getting members for organization: {OrganizationId}", organizationId);

        var cacheKey = CacheKeys.OrganizationMembers(organizationId);
        var cached = await _cacheService.GetAsync<List<OrganizationMember>>(cacheKey);
        if (cached != null)
        {
            _logger.LogTrace("Organization members for {OrganizationId} found in cache", organizationId);
            return cached;
        }

        var members = await _memberRepository.GetActiveOrganizationMembersAsync(organizationId);
        var memberList = members.ToList();

        if (memberList.Any())
        {
            await _cacheService.SetAsync(cacheKey, memberList, MembersCacheDuration);
            _logger.LogDebug("Found {Count} active members in organization {OrganizationId}", memberList.Count, organizationId);
        }
        else
        {
            _logger.LogDebug("No active members found in organization {OrganizationId}", organizationId);
        }

        return memberList;
    }

    public async Task<IEnumerable<OrganizationMember>> GetOrganizationMembersForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetOrganizationMembersForUserAsync called with null or empty userId");
            return Enumerable.Empty<OrganizationMember>();
        }

        _logger.LogTrace("Getting organization memberships for user: {UserId}", userId);

        var cacheKey = CacheKeys.UserMemberships(userId);
        var cached = await _cacheService.GetAsync<List<OrganizationMember>>(cacheKey);
        if (cached != null)
        {
            _logger.LogTrace("User memberships for {UserId} found in cache", userId);
            return cached;
        }

        var memberships = await _memberRepository.GetUserOrganizationMembershipsAsync(userId);
        var membershipList = memberships.ToList();

        if (membershipList.Any())
        {
            await _cacheService.SetAsync(cacheKey, membershipList, MembersCacheDuration);
            _logger.LogDebug("Found {Count} organization memberships for user {UserId}", membershipList.Count, userId);
        }
        else
        {
            _logger.LogDebug("No organization memberships found for user {UserId}", userId);
        }

        return membershipList;
    }

    public async Task<OrganizationMember?> GetMemberAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetMemberAsync called with null or empty userId");
            return null;
        }

        _logger.LogTrace("Getting member: {UserId} in organization {OrganizationId}", userId, organizationId);

        var member = await _memberRepository.GetMemberAsync(organizationId, userId);
        
        if (member?.IsActive == true)
        {
            _logger.LogTrace("Active member found: {UserId} in organization {OrganizationId}", userId, organizationId);
            return member;
        }
        
        _logger.LogDebug("No active member found: {UserId} in organization {OrganizationId}", userId, organizationId);
        return null;
    }

    public async Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("GetUserRoleAsync called with null or empty userId");
            return null;
        }

        _logger.LogTrace("Getting user role: {UserId} in organization {OrganizationId}", userId, organizationId);

        var role = await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId);
        
        if (role.HasValue)
        {
            _logger.LogTrace("User {UserId} has role {Role} in organization {OrganizationId}", userId, role, organizationId);
        }
        else
        {
            _logger.LogDebug("User {UserId} has no role in organization {OrganizationId}", userId, organizationId);
        }

        return role;
    }

    public async Task<(int current, int max)> GetProjectLimitsAsync(Guid organizationId)
    {
        _logger.LogTrace("Getting project limits for organization: {OrganizationId}", organizationId);

        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null)
        {
            _logger.LogWarning("Cannot get project limits for non-existent organization: {OrganizationId}", organizationId);
            throw new ArgumentException("Organization not found");
        }

        var current = await _organizationRepository.GetProjectCountAsync(organizationId);
        var max = organization.MaxProjects;

        _logger.LogDebug("Organization {OrganizationId} project limits: {Current}/{Max}", organizationId, current, max);
        
        return (current, max);
    }

    public async Task<(int current, int max)> GetMemberLimitsAsync(Guid organizationId)
    {
        _logger.LogTrace("Getting member limits for organization: {OrganizationId}", organizationId);

        var organization = await GetOrganizationAsync(organizationId);
        if (organization == null)
        {
            _logger.LogWarning("Cannot get member limits for non-existent organization: {OrganizationId}", organizationId);
            throw new ArgumentException("Organization not found");
        }

        var current = await _organizationRepository.GetMemberCountAsync(organizationId);
        var max = organization.MaxMembers;

        _logger.LogDebug("Organization {OrganizationId} member limits: {Current}/{Max}", organizationId, current, max);
        
        return (current, max);
    }
}