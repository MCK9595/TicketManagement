using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace TicketManagement.Web.Authentication;

public class AuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthorizationHandler> logger) 
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext 
            ?? throw new InvalidOperationException("No HttpContext available");

        var accessToken = await httpContext.GetTokenAsync("access_token");
        
        logger.LogInformation("AuthorizationHandler - Request: {Method} {Uri}", request.Method, request.RequestUri);
        logger.LogInformation("AuthorizationHandler - Access token present: {HasToken}", !string.IsNullOrWhiteSpace(accessToken));
        
        // Check if token is expired
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(accessToken);
                
                if (jsonToken.ValidTo < DateTime.UtcNow.AddMinutes(1)) // Token is expired or will expire in 1 minute
                {
                    logger.LogWarning("Token is expired or will expire soon. Expiry: {Expiry}, Current: {Current}", 
                        jsonToken.ValidTo, DateTime.UtcNow);
                    
                    // Don't attempt refresh in HTTP message handler - just log and continue
                    logger.LogInformation("Token will expire soon, API call may require re-authentication");
                }
                
                request.Headers.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                logger.LogInformation("AuthorizationHandler - Added Bearer token to request");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading JWT token");
            }
        }
        else
        {
            logger.LogWarning("AuthorizationHandler - No access token available for request to {Uri}", request.RequestUri);
        }

        var response = await base.SendAsync(request, cancellationToken);
        
        logger.LogDebug("AuthorizationHandler - Response: {StatusCode} for {Uri}", response.StatusCode, request.RequestUri);
        
        // If we get a 401, it might be due to an expired token
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("Received 401 Unauthorized. Token might be expired.");
        }
        
        return response;
    }
}