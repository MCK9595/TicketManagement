using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace TicketManagement.Web.Client.Authentication;

public class ServerAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerAuthenticationStateProvider> _logger;
    private bool _authenticationStateLoaded = false;
    private ClaimsPrincipal _cachedUser = new(new ClaimsIdentity());

    public ServerAuthenticationStateProvider(HttpClient httpClient, ILogger<ServerAuthenticationStateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_authenticationStateLoaded)
        {
            return new AuthenticationState(_cachedUser);
        }

        try
        {
            _logger.LogInformation("Checking authentication state from server");
            var response = await _httpClient.GetAsync("authentication/user");
            
            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
                
                if (userInfo?.IsAuthenticated == true && userInfo.Claims != null)
                {
                    var claims = userInfo.Claims.Select(c => new Claim(c.Type, c.Value)).ToArray();
                    var identity = new ClaimsIdentity(claims, "Server authentication", "name", "role");
                    _cachedUser = new ClaimsPrincipal(identity);
                    _logger.LogInformation("User authenticated with {ClaimCount} claims", claims.Length);
                }
                else
                {
                    _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
                    _logger.LogInformation("User not authenticated - no valid claims");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
                _logger.LogInformation("User not authenticated - 401 response");
            }
            else
            {
                _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
                _logger.LogWarning("Authentication check failed with status: {StatusCode}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error checking authentication state");
            _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking authentication state");
            _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        _authenticationStateLoaded = true;
        return new AuthenticationState(_cachedUser);
    }

    public void NotifyAuthenticationStateChanged()
    {
        _authenticationStateLoaded = false;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private class UserInfo
    {
        public bool IsAuthenticated { get; set; }
        public ClaimInfo[]? Claims { get; set; }
    }

    private class ClaimInfo
    {
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
    }
}