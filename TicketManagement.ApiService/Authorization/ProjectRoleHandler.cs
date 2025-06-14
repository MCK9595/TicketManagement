using Microsoft.AspNetCore.Authorization;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;
using System.Security.Claims;

namespace TicketManagement.ApiService.Authorization;

public class ProjectRoleHandler : AuthorizationHandler<ProjectRoleRequirement>
{
    private readonly IProjectService _projectService;
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<ProjectRoleHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectRoleHandler(
        IProjectService projectService,
        IOrganizationService organizationService,
        ILogger<ProjectRoleHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _projectService = projectService;
        _organizationService = organizationService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectRoleRequirement requirement)
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

            // Get project ID from context
            var projectId = GetProjectIdFromContext(context);

            if (!projectId.HasValue)
            {
                _logger.LogWarning("Project ID not found in context for user {UserId}", userId);
                return;
            }

            // Get project to find organization
            var project = await _projectService.GetProjectAsync(projectId.Value);
            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found", projectId.Value);
                return;
            }

            // Check user's role in the project first
            var projectRole = await _projectService.GetUserRoleInProjectAsync(projectId.Value, userId);
            
            if (projectRole.HasValue && projectRole.Value >= requirement.MinimumRole)
            {
                _logger.LogDebug("User {UserId} has project role {ProjectRole} in project {ProjectId}, required minimum: {MinimumRole}",
                    userId, projectRole.Value, projectId.Value, requirement.MinimumRole);
                
                context.Succeed(requirement);
                return;
            }

            // If no project role or insufficient project role, check organization role
            var organizationRole = await _organizationService.GetUserRoleAsync(project.OrganizationId, userId);
            
            if (organizationRole.HasValue)
            {
                // Organization Admins and Managers can act as Project Admins
                // Organization Members can act as Project Members
                // Organization Viewers can act as Project Viewers
                var effectiveProjectRole = MapOrganizationRoleToProjectRole(organizationRole.Value);
                
                if (effectiveProjectRole.HasValue && effectiveProjectRole.Value >= requirement.MinimumRole)
                {
                    _logger.LogDebug("User {UserId} has organization role {OrganizationRole} (effective project role: {EffectiveProjectRole}) in project {ProjectId}, required minimum: {MinimumRole}",
                        userId, organizationRole.Value, effectiveProjectRole.Value, projectId.Value, requirement.MinimumRole);
                    
                    context.Succeed(requirement);
                    return;
                }
            }

            _logger.LogWarning("User {UserId} does not have required role {MinimumRole} in project {ProjectId}. Project role: {ProjectRole}, Organization role: {OrganizationRole}",
                userId, requirement.MinimumRole, projectId.Value, projectRole, organizationRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking project role requirement");
        }
    }

    private ProjectRole? MapOrganizationRoleToProjectRole(OrganizationRole organizationRole)
    {
        return organizationRole switch
        {
            OrganizationRole.Admin => ProjectRole.Admin,
            OrganizationRole.Manager => ProjectRole.Admin,
            OrganizationRole.Member => ProjectRole.Member,
            OrganizationRole.Viewer => ProjectRole.Viewer,
            _ => null
        };
    }

    private Guid? GetProjectIdFromContext(AuthorizationHandlerContext context)
    {
        // Try to get project ID from different sources

        // 1. From resource (when explicitly passed)
        if (context.Resource is Guid projectIdResource)
        {
            return projectIdResource;
        }

        // 2. From HTTP context route values
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request.RouteValues.TryGetValue("projectId", out var projectIdRoute) == true)
        {
            if (Guid.TryParse(projectIdRoute?.ToString(), out var parsedProjectId))
            {
                return parsedProjectId;
            }
        }

        // 3. From HTTP context route values (alternative naming)
        if (httpContext?.Request.RouteValues.TryGetValue("id", out var idRoute) == true)
        {
            if (Guid.TryParse(idRoute?.ToString(), out var parsedId))
            {
                // Check if this is a project controller
                var controller = httpContext.Request.RouteValues["controller"]?.ToString();
                if (string.Equals(controller, "Projects", StringComparison.OrdinalIgnoreCase))
                {
                    return parsedId;
                }
            }
        }

        // 4. From query parameters
        if (httpContext?.Request.Query.TryGetValue("projectId", out var projectIdQuery) == true)
        {
            if (Guid.TryParse(projectIdQuery.FirstOrDefault(), out var parsedQueryProjectId))
            {
                return parsedQueryProjectId;
            }
        }

        // 5. From request headers (for API calls)
        if (httpContext?.Request.Headers.TryGetValue("X-Project-Id", out var projectIdHeader) == true)
        {
            if (Guid.TryParse(projectIdHeader.FirstOrDefault(), out var parsedHeaderProjectId))
            {
                return parsedHeaderProjectId;
            }
        }

        return null;
    }
}