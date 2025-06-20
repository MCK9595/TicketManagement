using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketManagement.Infrastructure.Data;
using TicketManagement.Contracts.Services;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Infrastructure.Services;
using TicketManagement.Infrastructure.Repositories;
using TicketManagement.ApiService.Hubs;
using TicketManagement.ApiService.Services;
using TicketManagement.ApiService.Middleware;
using TicketManagement.ApiService.Filters;
using TicketManagement.Infrastructure.Logging.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add database context with Aspire SQL Server integration
builder.AddSqlServerDbContext<TicketDbContext>("TicketDB", configureDbContextOptions: options =>
{
    // Query tracking behavior の最適化
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
});

// Temporarily disable Redis and use in-memory cache
builder.Services.AddDistributedMemoryCache();

// TODO: Re-enable Redis once connection issues are resolved
// builder.AddRedisDistributedCache("redis");

// Add services to the container.
builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        // In development, allow multiple possible frontend URLs
        var allowedOrigins = new List<string>();
        
        var configuredUrl = builder.Configuration["Frontend:Url"];
        if (!string.IsNullOrEmpty(configuredUrl))
        {
            allowedOrigins.Add(configuredUrl);
        }
        
        // Add common development URLs
        if (builder.Environment.IsDevelopment())
        {
            allowedOrigins.AddRange(new[] 
            {
                "https://localhost:7133",
                "http://localhost:7133",
                "https://localhost:5173",
                "http://localhost:5173",
                "https://localhost:40797",
                "http://localhost:37491"
            });
        }
        
        policy.WithOrigins(allowedOrigins.Distinct().ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials() // Required for SignalR
              .SetIsOriginAllowed(origin => builder.Environment.IsDevelopment()); // Allow any origin in development
    });
});

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Add JWT Bearer authentication with Keycloak integration
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "ticket-management",
        options =>
        {
            options.RequireHttpsMetadata = false; // Development environment
            // Temporarily disable audience validation for debugging
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
            
            // SignalR configuration
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "Authentication failed");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    if (context.SecurityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt)
                    {
                        logger.LogDebug("Token validated. Issuer: {Issuer}, Audience: {Audience}, Claims: {Claims}",
                            jwt.Issuer,
                            string.Join(", ", jwt.Audiences),
                            string.Join(", ", jwt.Claims.Select(c => $"{c.Type}={c.Value}")));
                    }
                    return Task.CompletedTask;
                }
            };
        });

// Add authorization with custom policies
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("ProjectManager", policy =>
        policy.RequireRole("project-manager", "admin"))
    .AddPolicy("TicketAssignee", policy =>
        policy.RequireRole("developer", "project-manager", "admin"))
    .AddPolicy("CommentAuthor", policy =>
        policy.RequireAuthenticatedUser())
    .AddPolicy("ProjectMember", policy =>
        policy.RequireAuthenticatedUser())
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"))
    
    // Organization-based policies
    .AddPolicy("OrganizationAdmin", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.OrganizationRoleRequirement(TicketManagement.Core.Enums.OrganizationRole.Admin)))
    .AddPolicy("OrganizationManager", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.OrganizationRoleRequirement(TicketManagement.Core.Enums.OrganizationRole.Manager)))
    .AddPolicy("OrganizationMember", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.OrganizationRoleRequirement(TicketManagement.Core.Enums.OrganizationRole.Member)))
    .AddPolicy("OrganizationViewer", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.OrganizationRoleRequirement(TicketManagement.Core.Enums.OrganizationRole.Viewer)))
    
    // Project-based policies  
    .AddPolicy("ProjectAdmin", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.ProjectRoleRequirement(TicketManagement.Core.Enums.ProjectRole.Admin)))
    .AddPolicy("ProjectMemberRole", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.ProjectRoleRequirement(TicketManagement.Core.Enums.ProjectRole.Member)))
    .AddPolicy("ProjectViewer", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.ProjectRoleRequirement(TicketManagement.Core.Enums.ProjectRole.Viewer)))
    
    // System-level policies
    .AddPolicy("SystemAdmin", policy =>
        policy.Requirements.Add(new TicketManagement.ApiService.Authorization.SystemRoleRequirement(TicketManagement.Core.Enums.SystemRole.SystemAdmin)));

// Register authorization handlers
builder.Services.AddScoped<IAuthorizationHandler, TicketManagement.ApiService.Authorization.OrganizationRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TicketManagement.ApiService.Authorization.ProjectRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TicketManagement.ApiService.Authorization.SystemRoleHandler>();

// Register repositories
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IOrganizationMemberRepository, OrganizationMemberRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<ITicketAssignmentRepository, TicketAssignmentRepository>();
builder.Services.AddScoped<ITicketHistoryRepository, TicketHistoryRepository>();

// Add comprehensive logging system
builder.Services.AddStructuredLogging(builder.Configuration);

// Register services with improved lifetime management
builder.Services.AddSingleton<ICacheService, CacheService>(); // Cache service should be singleton
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Register CQRS-based organization services
builder.Services.AddScoped<TicketManagement.Contracts.Commands.IOrganizationCommandService, 
    TicketManagement.Infrastructure.Services.Commands.OrganizationCommandService>();
builder.Services.AddScoped<TicketManagement.Contracts.Queries.IOrganizationQueryService, 
    TicketManagement.Infrastructure.Services.Queries.OrganizationQueryService>();
builder.Services.AddScoped<TicketManagement.Contracts.Queries.IOrganizationAuthorizationService, 
    TicketManagement.Infrastructure.Services.Authorization.OrganizationAuthorizationService>();

// Register composite service for backward compatibility
builder.Services.AddScoped<IOrganizationService, TicketManagement.Infrastructure.Services.OrganizationServiceComposite>();

// Register other services (to be refactored)
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IProjectService, ProjectService>();

// Register HttpClient for UserManagementService
builder.Services.AddHttpClient<UserManagementService>();

// Add controllers and API endpoints with global filters
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ModelValidationFilter>();
});

// Configure JSON options for security
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

// Add HTTP context accessor for accessing user context in services
builder.Services.AddHttpContextAccessor();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Swagger/OpenAPI with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token."
    });
    
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

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

// Add authentication/authorization debugging middleware in development
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        
        logger.LogDebug("Request Path: {Path}", context.Request.Path);
        logger.LogDebug("User authenticated: {IsAuthenticated}", context.User.Identity?.IsAuthenticated);
        
        if (context.User.Identity?.IsAuthenticated == true)
        {
            logger.LogDebug("User claims: {Claims}", 
                string.Join(", ", context.User.Claims.Select(c => $"{c.Type}={c.Value}")));
        }
        
        await next();
        
        if (context.Response.StatusCode == 401)
        {
            logger.LogWarning("Unauthorized response for path: {Path}", context.Request.Path);
        }
    });
}

// Add comprehensive logging middleware
app.UseEnhancedLogging();
app.UsePerformanceMonitoring();

// Add security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Add request logging
app.UseRequestLogging();

// Add rate limiting
app.UseRateLimiting();

// Use CORS
app.UseCors("DefaultPolicy");

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthenticationLogging();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TicketManagement API V1");
        c.RoutePrefix = "swagger";
    });
}

// Map controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notifications");

// User info endpoint (protected)
app.MapGet("/api/user/info", (ClaimsPrincipal user) =>
{
    if (!user.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();

    var userInfo = new
    {
        Id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value,
        Email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value,
        Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
        PreferredUsername = user.FindFirst("preferred_username")?.Value,
        Claims = user.Claims.ToDictionary(c => c.Type, c => c.Value)
    };

    return Results.Ok(userInfo);
})
.RequireAuthorization()
.WithName("GetUserInfo")
.WithOpenApi();

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }
