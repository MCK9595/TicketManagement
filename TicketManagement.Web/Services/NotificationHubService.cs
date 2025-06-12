using Microsoft.AspNetCore.SignalR.Client;
using TicketManagement.Contracts.DTOs;

namespace TicketManagement.Web.Services;

public class NotificationHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ILogger<NotificationHubService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationHubService(ILogger<NotificationHubService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public event Action<NotificationDto>? NotificationReceived;
    public event Action<int>? UnreadCountUpdated;

    public async Task StartAsync(string apiBaseUrl, string? accessToken = null)
    {
        // Handle Aspire service discovery scheme conversion
        var resolvedUrl = apiBaseUrl;
        
        // If using Aspire service discovery, replace the scheme and service name with actual URL
        if (apiBaseUrl.StartsWith("https+http://"))
        {
            // In development, use the known API service URL
            resolvedUrl = "https://localhost:7521";
            _logger.LogInformation("Using development URL for SignalR: {ResolvedUrl}", resolvedUrl);
        }
        
        var hubUrl = $"{resolvedUrl.TrimEnd('/')}/hubs/notifications";
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<NotificationDto>("ReceiveNotification", (notification) =>
        {
            _logger.LogInformation("Received notification: {Message}", notification.Message);
            NotificationReceived?.Invoke(notification);
        });

        _hubConnection.On<int>("UpdateUnreadCount", (count) =>
        {
            _logger.LogInformation("Unread count updated: {Count}", count);
            UnreadCountUpdated?.Invoke(count);
        });

        _hubConnection.Reconnecting += (exception) =>
        {
            _logger.LogWarning("SignalR reconnecting: {Exception}", exception?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += (exception) =>
        {
            _logger.LogWarning("SignalR connection closed: {Exception}", exception?.Message);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting SignalR connection");
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                return await _hubConnection.InvokeAsync<int>("GetUnreadCount");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
            }
        }
        return 0;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("MarkAsRead", notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
            }
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("MarkAllAsRead");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
            }
        }
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;


    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}