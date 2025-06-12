using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Web.Authentication;

namespace TicketManagement.Web.Controllers;

[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IConfiguration configuration, ILogger<AccountController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Challenge(string? returnUrl = null)
    {
        // Check if Keycloak is properly configured
        if (!await IsKeycloakConfiguredAndHealthy())
        {
            _logger.LogError("Keycloak is not properly configured or unavailable");
            return RedirectToAction("Error", "Home", new { message = "認証サーバーが利用できません。システム管理者にお問い合わせください。" });
        }
        
        var redirectUrl = returnUrl ?? Url.Content("~/");
        
        try
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = redirectUrl },
                OpenIdConnectDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Keycloak authentication challenge");
            return RedirectToAction("Error", "Home", new { message = "認証の開始に失敗しました。" });
        }
    }



    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var callbackUrl = Url.Action("SignedOut") ?? "/";
        
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, 
            new AuthenticationProperties { RedirectUri = callbackUrl });
        
        return new SignOutResult(
            new[] { CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme },
            new AuthenticationProperties { RedirectUri = callbackUrl });
    }

    [HttpGet]
    public IActionResult SignedOut()
    {
        if (HttpContext.User.Identity?.IsAuthenticated == true)
        {
            // Redirect to home page if the user is authenticated.
            return Redirect("~/");
        }

        return Redirect("~/login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Access denied for user: {UserId}", HttpContext.User.Identity?.Name ?? "Anonymous");
        return View("AccessDenied");
    }

    private async Task<bool> IsKeycloakConfiguredAndHealthy()
    {
        try
        {
            var keycloakBaseUrl = _configuration.GetConnectionString("keycloak") ?? 
                                _configuration["Authentication:Keycloak:BaseUrl"];
            
            if (string.IsNullOrEmpty(keycloakBaseUrl))
            {
                _logger.LogWarning("Keycloak base URL is not configured");
                return false;
            }

            var realm = _configuration["Authentication:Keycloak:Realm"];
            var clientId = _configuration["Authentication:Keycloak:ClientId"];

            if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogWarning("Keycloak realm or client ID is not configured");
                return false;
            }

            // Quick health check with increased timeout
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var response = await client.GetAsync($"{keycloakBaseUrl}/realms/{realm}");
            
            var isHealthy = response.IsSuccessStatusCode;
            
            if (!isHealthy)
            {
                _logger.LogWarning("Keycloak health check failed with status: {StatusCode}", response.StatusCode);
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Keycloak health: {Error}", ex.Message);
            return false;
        }
    }
}