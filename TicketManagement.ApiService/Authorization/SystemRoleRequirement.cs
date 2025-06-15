using Microsoft.AspNetCore.Authorization;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Authorization;

public class SystemRoleRequirement : IAuthorizationRequirement
{
    public SystemRole RequiredRole { get; }

    public SystemRoleRequirement(SystemRole requiredRole)
    {
        RequiredRole = requiredRole;
    }
}