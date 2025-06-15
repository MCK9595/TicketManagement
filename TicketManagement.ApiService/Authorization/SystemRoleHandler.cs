using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.ApiService.Authorization;

public class SystemRoleHandler : AuthorizationHandler<SystemRoleRequirement>
{
    private readonly TicketDbContext _context;
    private readonly ILogger<SystemRoleHandler> _logger;

    public SystemRoleHandler(TicketDbContext context, ILogger<SystemRoleHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemRoleRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value ??
                    context.User.FindFirst("user_id")?.Value ??
                    context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User ID not found in claims for system role check");
            return;
        }

        try
        {
            var hasSystemAdminRole = await _context.SystemAdmins
                .AnyAsync(sa => sa.UserId == userId && sa.IsActive);

            if (hasSystemAdminRole && requirement.RequiredRole <= SystemRole.SystemAdmin)
            {
                context.Succeed(requirement);
                return;
            }

            // Check if user has OrganizationAdmin role in any organization
            if (requirement.RequiredRole <= SystemRole.OrganizationAdmin)
            {
                var hasOrgAdminRole = await _context.OrganizationMembers
                    .AnyAsync(om => om.UserId == userId && om.Role == OrganizationRole.Admin);

                if (hasOrgAdminRole)
                {
                    context.Succeed(requirement);
                    return;
                }
            }

            _logger.LogWarning("User {UserId} does not have required system role {RequiredRole}", 
                userId, requirement.RequiredRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system role for user {UserId}", userId);
        }
    }
}