using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

namespace TicketManagement.Web.Client.Authentication;

public class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILogger<AuthenticatedHttpClientHandler> _logger;

    public AuthenticatedHttpClientHandler(
        AuthenticationStateProvider authenticationStateProvider,
        ILogger<AuthenticatedHttpClientHandler> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                // In WebAssembly, we'll use cookies for authentication instead of Bearer tokens
                // The authentication cookie will be automatically included in requests
                _logger.LogDebug("User is authenticated, request will include authentication cookies");
            }
            else
            {
                _logger.LogDebug("User is not authenticated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication state for HTTP request");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}