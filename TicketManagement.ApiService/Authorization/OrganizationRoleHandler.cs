using Microsoft.AspNetCore.Authorization;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;
using System.Security.Claims;

namespace TicketManagement.ApiService.Authorization;

public class OrganizationRoleHandler : AuthorizationHandler<OrganizationRoleRequirement>
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationRoleHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrganizationRoleHandler(
        IOrganizationService organizationService,
        ILogger<OrganizationRoleHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _organizationService = organizationService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationRoleRequirement requirement)
    {
        try
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? context.User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims");
                return;
            }

            // Get organization ID from various sources
            var organizationId = GetOrganizationIdFromContext(context);

            if (!organizationId.HasValue)
            {
                _logger.LogWarning("Organization ID not found in context for user {UserId}", userId);
                return;
            }

            // Check user's role in the organization
            var userRole = await _organizationService.GetUserRoleAsync(organizationId.Value, userId);

            if (userRole.HasValue && userRole.Value >= requirement.MinimumRole)
            {
                _logger.LogDebug("User {UserId} has role {UserRole} in organization {OrganizationId}, required minimum: {MinimumRole}",
                    userId, userRole.Value, organizationId.Value, requirement.MinimumRole);
                
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} does not have required role {MinimumRole} in organization {OrganizationId}. Current role: {UserRole}",
                    userId, requirement.MinimumRole, organizationId.Value, userRole);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking organization role requirement");
        }
    }

    private Guid? GetOrganizationIdFromContext(AuthorizationHandlerContext context)
    {
        // Try to get organization ID from different sources

        // 1. From resource (when explicitly passed)
        if (context.Resource is Guid orgIdResource)
        {
            return orgIdResource;
        }

        // 2. From HTTP context route values
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request.RouteValues.TryGetValue("organizationId", out var orgIdRoute) == true)
        {
            if (Guid.TryParse(orgIdRoute?.ToString(), out var parsedOrgId))
            {
                return parsedOrgId;
            }
        }

        // 3. From HTTP context route values (alternative naming)
        if (httpContext?.Request.RouteValues.TryGetValue("id", out var idRoute) == true)
        {
            if (Guid.TryParse(idRoute?.ToString(), out var parsedId))
            {
                // Check if this is an organization controller
                var controller = httpContext.Request.RouteValues["controller"]?.ToString();
                if (string.Equals(controller, "Organizations", StringComparison.OrdinalIgnoreCase))
                {
                    return parsedId;
                }
            }
        }

        // 4. From query parameters
        if (httpContext?.Request.Query.TryGetValue("organizationId", out var orgIdQuery) == true)
        {
            if (Guid.TryParse(orgIdQuery.FirstOrDefault(), out var parsedQueryOrgId))
            {
                return parsedQueryOrgId;
            }
        }

        // 5. From request headers (for API calls)
        if (httpContext?.Request.Headers.TryGetValue("X-Organization-Id", out var orgIdHeader) == true)
        {
            if (Guid.TryParse(orgIdHeader.FirstOrDefault(), out var parsedHeaderOrgId))
            {
                return parsedHeaderOrgId;
            }
        }

        return null;
    }
}