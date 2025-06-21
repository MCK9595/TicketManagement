using TicketManagement.Web.Components;
using TicketManagement.Web.Services;
using TicketManagement.Web.Client.Services;
using TicketManagement.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

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
builder.Services.AddHttpClient<TicketManagement.Web.Client.Services.TicketManagementApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Allow HTTP/2 and HTTP/1.1
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
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
            
            // Disable GetClaimsFromUserInfoEndpoint to avoid 401 errors
            // We get sufficient claims from the ID token including roles
            options.GetClaimsFromUserInfoEndpoint = false;
            
            // Note: If PAR issues persist, the redirect URI configuration in Events should handle it
            
            // Configure events to set redirect URI and handle role mapping
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
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Web: OnTokenValidated called");
                    
                    // Get the ClaimsIdentity
                    var identity = context.Principal?.Identity as ClaimsIdentity;
                    if (identity != null)
                    {
                        // Log all initial claims for debugging
                        logger.LogInformation("Initial claims before role processing:");
                        foreach (var claim in identity.Claims)
                        {
                            logger.LogInformation("Initial Claim: {Type} = {Value}", claim.Type, claim.Value);
                        }
                        
                        // Extract roles from realm_access claims (individual role claims)
                        var realmAccessClaims = identity.FindAll("realm_access").ToList();
                        if (realmAccessClaims.Any())
                        {
                            logger.LogInformation("Found {Count} realm_access claims", realmAccessClaims.Count);
                            foreach (var realmAccessClaim in realmAccessClaims)
                            {
                                var roleValue = realmAccessClaim.Value;
                                if (!string.IsNullOrEmpty(roleValue))
                                {
                                    // Add the role directly as it's already a string value
                                    identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                                    logger.LogInformation("Web: Added role claim from realm_access: {Role}", roleValue);
                                }
                            }
                        }
                        else
                        {
                            logger.LogWarning("No realm_access claim found in ID token, checking access token");
                            
                            // Try to get roles from access token
                            try
                            {
                                var accessToken = context.Properties.GetTokenValue("access_token");
                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                                    var jsonToken = handler.ReadJwtToken(accessToken);
                                    
                                    logger.LogInformation("Access token claims:");
                                    foreach (var claim in jsonToken.Claims)
                                    {
                                        logger.LogInformation("Access Token Claim: {Type} = {Value}", claim.Type, claim.Value);
                                    }
                                    
                                    // Check for realm_access in access token
                                    var accessTokenRealmAccess = jsonToken.Claims.FirstOrDefault(c => c.Type == "realm_access");
                                    if (accessTokenRealmAccess != null)
                                    {
                                        logger.LogInformation("Found realm_access in access token: {Value}", accessTokenRealmAccess.Value);
                                        var realmAccess = System.Text.Json.JsonDocument.Parse(accessTokenRealmAccess.Value);
                                        if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                                        {
                                            foreach (var role in rolesElement.EnumerateArray())
                                            {
                                                var roleValue = role.GetString();
                                                if (!string.IsNullOrEmpty(roleValue))
                                                {
                                                    identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                                                    logger.LogInformation("Web: Added role claim from access token: {Role}", roleValue);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: check for individual role claims in access token
                                        var roleClaims = jsonToken.Claims.Where(c => c.Type == "roles").ToList();
                                        foreach (var roleClaim in roleClaims)
                                        {
                                            identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                                            logger.LogInformation("Web: Added individual role claim: {Role}", roleClaim.Value);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error processing access token for roles");
                            }
                        }
                        
                        // Log all final claims for debugging
                        logger.LogInformation("Final claims after role processing:");
                        foreach (var claim in identity.Claims)
                        {
                            logger.LogInformation("Final Claim: {Type} = {Value}", claim.Type, claim.Value);
                        }
                    }
                    else
                    {
                        logger.LogWarning("No ClaimsIdentity found in context.Principal");
                    }
                    
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
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add MVC for authentication controllers
builder.Services.AddControllers();

// Add authorization with global policy
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthentication", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("SystemAdmin", policy => policy.RequireRole("system-admin"));

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();


// Add SignalR notification service
builder.Services.AddScoped<TicketManagement.Web.Client.Services.NotificationHubService>();

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
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TicketManagement.Web.Client._Imports).Assembly);

// Map authentication state provider for WebAssembly
app.MapGet("authentication/user", async (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var claims = context.User.Claims.Select(c => new { c.Type, c.Value }).ToArray();
        return Results.Json(new { IsAuthenticated = true, Claims = claims });
    }
    
    return Results.Json(new { IsAuthenticated = false, Claims = Array.Empty<object>() });
}).AllowAnonymous();


// Map controller routes for authentication
app.MapControllers();

app.MapDefaultEndpoints();

app.Run();
