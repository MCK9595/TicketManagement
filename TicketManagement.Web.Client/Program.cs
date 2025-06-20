using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using TicketManagement.Web.Client.Services;
using TicketManagement.Web.Client.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add authentication services
builder.Services.AddAuthorizationCore();

// Configure a basic HttpClient for authentication checks (no circular dependency)
builder.Services.AddHttpClient("AuthClient", client => 
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

// Register custom AuthenticationStateProvider with its own HttpClient
builder.Services.AddScoped<ServerAuthenticationStateProvider>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("AuthClient");
    var logger = sp.GetRequiredService<ILogger<ServerAuthenticationStateProvider>>();
    return new ServerAuthenticationStateProvider(httpClient, logger);
});

// Register as AuthenticationStateProvider
builder.Services.AddScoped<AuthenticationStateProvider>(sp => 
    sp.GetRequiredService<ServerAuthenticationStateProvider>());

// Add cascading authentication state
builder.Services.AddCascadingAuthenticationState();

// Register authentication handler AFTER AuthenticationStateProvider
builder.Services.AddScoped<AuthenticatedHttpClientHandler>();

// Configure HttpClient for API calls with authentication
builder.Services.AddHttpClient<TicketManagementApiClient>(client => 
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

// Register NotificationHubService
builder.Services.AddScoped<NotificationHubService>();

await builder.Build().RunAsync();