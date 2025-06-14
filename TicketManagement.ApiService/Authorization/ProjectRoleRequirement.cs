using Microsoft.AspNetCore.Authorization;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Authorization;

public class ProjectRoleRequirement : IAuthorizationRequirement
{
    public ProjectRole MinimumRole { get; }

    public ProjectRoleRequirement(ProjectRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}