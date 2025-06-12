using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Net;

namespace TicketManagement.Web.Authentication;

public class KeycloakAuthenticationEvents : OpenIdConnectEvents
{
    private readonly ILogger<KeycloakAuthenticationEvents> _logger;
    private readonly IConfiguration _configuration;

    public KeycloakAuthenticationEvents(ILogger<KeycloakAuthenticationEvents> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        _logger.LogError(context.Exception, "Keycloak authentication failed: {Error}", context.Exception.Message);

        // Handle different types of authentication failures
        var errorMessage = GetUserFriendlyErrorMessage(context.Exception);
        var fallbackUrl = "/login?error=" + Uri.EscapeDataString(errorMessage);

        context.Response.Redirect(fallbackUrl);
        context.HandleResponse();

        return Task.CompletedTask;
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        _logger.LogError(context.Failure, "Keycloak remote authentication failure: {Error}", context.Failure?.Message);

        var errorMessage = GetUserFriendlyErrorMessage(context.Failure);
        var fallbackUrl = "/login?error=" + Uri.EscapeDataString(errorMessage);

        context.Response.Redirect(fallbackUrl);
        context.HandleResponse();

        return Task.CompletedTask;
    }


    public override Task TokenValidated(TokenValidatedContext context)
    {
        _logger.LogInformation("Token validated successfully for user: {UserId}", 
            context.Principal?.Identity?.Name ?? "Unknown");
        return Task.CompletedTask;
    }

    public override Task UserInformationReceived(UserInformationReceivedContext context)
    {
        _logger.LogDebug("User information received from Keycloak for user: {UserId}", 
            context.User.RootElement.TryGetProperty("preferred_username", out var usernameProperty) 
                ? usernameProperty.GetString() 
                : "Unknown");
        return Task.CompletedTask;
    }

    private string GetUserFriendlyErrorMessage(Exception? exception)
    {
        return exception switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") =>
                "認証サーバーへの接続がタイムアウトしました。しばらく後に再試行してください。",
            
            HttpRequestException httpEx when httpEx.Message.Contains("refused") =>
                "認証サーバーに接続できません。システム管理者にお問い合わせください。",
            
            InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("configuration") =>
                "認証設定に問題があります。システム管理者にお問い合わせください。",
            
            _ when exception?.Message.Contains("invalid_client") == true =>
                "認証設定が正しくありません。システム管理者にお問い合わせください。",
            
            _ when exception?.Message.Contains("access_denied") == true =>
                "アクセスが拒否されました。適切な権限があることを確認してください。",
            
            _ when exception?.Message.Contains("invalid_grant") == true =>
                "認証情報が無効です。再度ログインしてください。",
            
            _ => "認証中にエラーが発生しました。しばらく後に再試行してください。"
        };
    }


    private async Task<bool> IsKeycloakHealthyAsync()
    {
        try
        {
            var keycloakBaseUrl = _configuration.GetConnectionString("keycloak") ?? 
                                _configuration["Authentication:Keycloak:BaseUrl"];
            
            if (string.IsNullOrEmpty(keycloakBaseUrl)) return false;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{keycloakBaseUrl}/realms/{_configuration["Authentication:Keycloak:Realm"]}");
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak health check failed");
            return false;
        }
    }
}