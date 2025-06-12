using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace TicketManagement.Web.Authentication;

public class KeycloakAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<KeycloakAuthenticationMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public KeycloakAuthenticationMiddleware(RequestDelegate next, ILogger<KeycloakAuthenticationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Check if this is a protected resource that requires authentication
            var endpoint = context.GetEndpoint();
            var requiresAuth = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() != null;


            await _next(context);
        }
        catch (InvalidOperationException authEx) when (authEx.Message.Contains("authentication"))
        {
            _logger.LogError(authEx, "Authentication error occurred: {Error}", authEx.Message);
            HandleAuthenticationError(context, authEx);
        }
        catch (Exception ex) when (IsAuthenticationRelated(ex))
        {
            _logger.LogError(ex, "Authentication-related error occurred: {Error}", ex.Message);
            HandleAuthenticationError(context, ex);
        }
    }

    private async Task<bool> IsKeycloakAvailableAsync()
    {
        try
        {
            var keycloakBaseUrl = _configuration.GetConnectionString("keycloak") ?? 
                                _configuration["Authentication:Keycloak:BaseUrl"];
            
            if (string.IsNullOrEmpty(keycloakBaseUrl)) return false;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var realm = _configuration["Authentication:Keycloak:Realm"] ?? "ticket-management";
            var response = await client.GetAsync($"{keycloakBaseUrl}/realms/{realm}");
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }


    private void HandleAuthenticationError(HttpContext context, Exception exception)
    {
        // Don't handle errors if response has already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Cannot handle authentication error - response has already started");
            return;
        }

        var errorMessage = GetUserFriendlyErrorMessage(exception);

        // Redirect to login with error message
        var loginUrl = "/login?error=" + Uri.EscapeDataString(errorMessage);
        context.Response.Redirect(loginUrl);
    }

    private string GetUserFriendlyErrorMessage(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException authEx when authEx.Message.Contains("token") =>
                "認証トークンが無効です。再度ログインしてください。",
            
            InvalidOperationException authEx when authEx.Message.Contains("expired") =>
                "セッションが期限切れです。再度ログインしてください。",
            
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") =>
                "認証サーバーへの接続がタイムアウトしました。",
            
            HttpRequestException httpEx when httpEx.Message.Contains("refused") =>
                "認証サーバーに接続できません。",
            
            _ => "認証エラーが発生しました。再度お試しください。"
        };
    }

    private bool IsAuthenticationRelated(Exception exception)
    {
        return exception is HttpRequestException httpEx && 
               (httpEx.Message.Contains("auth") || 
                httpEx.Message.Contains("token") || 
                httpEx.Message.Contains("unauthorized"));
    }

}