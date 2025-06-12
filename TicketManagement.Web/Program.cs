using TicketManagement.Web.Components;
using TicketManagement.Web.Services;
using TicketManagement.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Temporarily disable Redis and use in-memory cache
builder.Services.AddOutputCache();

// TODO: Re-enable Redis once connection issues are resolved
// builder.AddRedisOutputCache("redis");

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add custom authorization handler
builder.Services.AddTransient<AuthorizationHandler>();

// Configure HttpClient for API calls with authorization
builder.Services.AddHttpClient<TicketManagementApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
})
.AddHttpMessageHandler<AuthorizationHandler>();

// Add OpenID Connect authentication with Keycloak
var oidcScheme = OpenIdConnectDefaults.AuthenticationScheme;

builder.Services.AddAuthentication(oidcScheme)
    .AddKeycloakOpenIdConnect(
        serviceName: "keycloak",
        realm: "ticket-management",
        oidcScheme,
        options =>
        {
            options.ClientId = "ticket-management-web";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.RequireHttpsMetadata = false;
            options.SaveTokens = true;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.CallbackPath = "/signin-oidc";
            
            // Add scopes for API access
            // Removed offline_access scope as it was causing authentication issues
            
            // Set GetClaimsFromUserInfoEndpoint to true to get user claims
            options.GetClaimsFromUserInfoEndpoint = true;
            
            // Note: If PAR issues persist, the redirect URI configuration in Events should handle it
            
            // Configure events to set redirect URI
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    // Get the actual request URI to build proper redirect URI
                    var request = context.Request;
                    var redirectUri = $"{request.Scheme}://{request.Host}{context.Options.CallbackPath}";
                    context.ProtocolMessage.RedirectUri = redirectUri;
                    
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Setting RedirectUri to: {RedirectUri}", redirectUri);
                    logger.LogInformation("Request Host: {Host}, Scheme: {Scheme}, CallbackPath: {CallbackPath}", 
                        request.Host, request.Scheme, context.Options.CallbackPath);
                    
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProviderForSignOut = context =>
                {
                    var request = context.Request;
                    var logoutRedirectUri = $"{request.Scheme}://{request.Host}/Account/SignedOut";
                    context.ProtocolMessage.PostLogoutRedirectUri = logoutRedirectUri;
                    
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Setting PostLogoutRedirectUri to: {RedirectUri}", logoutRedirectUri);
                    
                    return Task.CompletedTask;
                }
            };
        })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        
        // Handle expired tokens
        options.Events.OnValidatePrincipal = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            try
            {
                // Check if the authentication properties contain the access token
                var accessToken = context.Properties.GetTokenValue("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadJwtToken(accessToken);
                    
                    if (jsonToken.ValidTo < DateTime.UtcNow)
                    {
                        logger.LogWarning("Access token expired. Redirecting to login.");
                        context.RejectPrincipal();
                        return Task.CompletedTask;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating token");
                context.RejectPrincipal();
                return Task.CompletedTask;
            }
            
            return Task.CompletedTask;
        };
    });

// Register authentication event handler
builder.Services.AddScoped<KeycloakAuthenticationEvents>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MVC for authentication controllers
builder.Services.AddControllers();

// Add authorization with global policy
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthentication", policy => policy.RequireAuthenticatedUser());

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();


// Add SignalR notification service
builder.Services.AddScoped<NotificationHubService>();

// Add HTTP client factory for health checks
builder.Services.AddHttpClient();

// Add Keycloak health check
builder.Services.AddHealthChecks()
    .AddCheck<KeycloakHealthCheck>("keycloak", 
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "authentication" });

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);

// Early health check middleware to bypass other middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health") && context.Request.Method == "GET")
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new 
        { 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow 
        }));
        return;
    }
    await next();
});

app.UseHttpsRedirection();

app.UseAuthentication();

// Add custom authentication middleware for error handling
app.UseMiddleware<KeycloakAuthenticationMiddleware>();

app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

// Authentication endpoints
var authGroup = app.MapGroup("authentication");

authGroup.MapGet("/login", () => 
    Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [oidcScheme]))
    .AllowAnonymous();

authGroup.MapPost("/logout", () => 
    Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, oidcScheme]));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map controller routes for authentication
app.MapControllers();

app.MapDefaultEndpoints();

app.Run();
