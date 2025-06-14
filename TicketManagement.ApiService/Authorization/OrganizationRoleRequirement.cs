using Microsoft.AspNetCore.Authorization;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Authorization;

public class OrganizationRoleRequirement : IAuthorizationRequirement
{
    public OrganizationRole MinimumRole { get; }

    public OrganizationRoleRequirement(OrganizationRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}