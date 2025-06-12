using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TicketManagement.Web.Authentication;

public class KeycloakHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakHealthCheck> _logger;

    public KeycloakHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<KeycloakHealthCheck> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var keycloakBaseUrl = _configuration.GetConnectionString("keycloak") ?? 
                                _configuration["Authentication:Keycloak:BaseUrl"];
            
            if (string.IsNullOrEmpty(keycloakBaseUrl))
            {
                return HealthCheckResult.Unhealthy("Keycloak base URL is not configured");
            }

            var realm = _configuration["Authentication:Keycloak:Realm"] ?? "ticket-management";
            var clientId = _configuration["Authentication:Keycloak:ClientId"] ?? "ticket-management-web";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Check if Keycloak server is responding
            var serverCheckUrl = $"{keycloakBaseUrl}/realms/{realm}";
            var serverResponse = await httpClient.GetAsync(serverCheckUrl, cancellationToken);

            if (!serverResponse.IsSuccessStatusCode)
            {
                var errorMessage = $"Keycloak server returned {serverResponse.StatusCode}";
                _logger.LogWarning("Keycloak health check failed: {Error}", errorMessage);
                return HealthCheckResult.Unhealthy(errorMessage);
            }

            // Check if essential endpoints are available (skip OpenID auto-discovery)
            var authEndpointUrl = $"{keycloakBaseUrl}/realms/{realm}/protocol/openid-connect/auth";
            var authResponse = await httpClient.GetAsync(authEndpointUrl, cancellationToken);

            if (authResponse.StatusCode != System.Net.HttpStatusCode.BadRequest && 
                authResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var errorMessage = $"Keycloak auth endpoint check failed with status {authResponse.StatusCode}";
                _logger.LogWarning("Keycloak auth endpoint check failed: {Error}", errorMessage);
                return HealthCheckResult.Degraded(errorMessage);
            }

            var responseTime = await MeasureResponseTime(httpClient, serverCheckUrl, cancellationToken);
            
            var data = new Dictionary<string, object>
            {
                ["keycloak_url"] = keycloakBaseUrl,
                ["realm"] = realm,
                ["client_id"] = clientId,
                ["response_time_ms"] = responseTime,
                ["check_time"] = DateTime.UtcNow
            };

            if (responseTime > 5000) // 5 seconds
            {
                return HealthCheckResult.Degraded($"Keycloak is responding slowly ({responseTime}ms)", data: data);
            }

            _logger.LogDebug("Keycloak health check passed. Response time: {ResponseTime}ms", responseTime);
            return HealthCheckResult.Healthy($"Keycloak is healthy (response time: {responseTime}ms)", data: data);
        }
        catch (TaskCanceledException)
        {
            var errorMessage = "Keycloak health check timed out";
            _logger.LogWarning(errorMessage);
            return HealthCheckResult.Unhealthy(errorMessage);
        }
        catch (HttpRequestException httpEx)
        {
            var errorMessage = $"Keycloak connection failed: {httpEx.Message}";
            _logger.LogWarning(httpEx, "Keycloak health check failed: {Error}", errorMessage);
            return HealthCheckResult.Unhealthy(errorMessage);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Keycloak health check failed: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during Keycloak health check: {Error}", errorMessage);
            return HealthCheckResult.Unhealthy(errorMessage);
        }
    }

    private async Task<long> MeasureResponseTime(HttpClient httpClient, string url, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await httpClient.GetAsync(url, cancellationToken);
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }
}