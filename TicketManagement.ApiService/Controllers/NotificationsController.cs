using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ITicketService _ticketService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ITicketService ticketService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _ticketService = ticketService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// ユーザーの通知一覧を取得（ページング付き）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<PagedResultDto<NotificationDto>>>> GetNotifications(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _notificationService.GetPagedNotificationsAsync(userId, page, pageSize);

            var notificationDtos = result.Items.Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                RelatedTicketId = n.RelatedTicketId,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                RelatedTicketTitle = null // TODO: チケットタイトルを取得する実装が必要
            }).ToList();

            var pagedResult = new PagedResultDto<NotificationDto>
            {
                Items = notificationDtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            return ApiResponseDto<PagedResultDto<NotificationDto>>.SuccessResult(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user");
            return StatusCode(500, ApiResponseDto<PagedResultDto<NotificationDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 未読通知一覧を取得
    /// </summary>
    [HttpGet("unread")]
    public async Task<ActionResult<ApiResponseDto<List<NotificationDto>>>> GetUnreadNotifications()
    {
        try
        {
            var userId = GetCurrentUserId();
            var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);

            var notificationDtos = notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                RelatedTicketId = n.RelatedTicketId,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                RelatedTicketTitle = null // TODO: チケットタイトルを取得する実装が必要
            }).ToList();

            return ApiResponseDto<List<NotificationDto>>.SuccessResult(notificationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notifications for user");
            return StatusCode(500, ApiResponseDto<List<NotificationDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 通知サマリーを取得（未読数と最近の通知）
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponseDto<NotificationSummaryDto>>> GetNotificationSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            var recentNotifications = await _notificationService.GetUserNotificationsAsync(userId);

            var summary = new NotificationSummaryDto
            {
                UnreadCount = unreadCount,
                RecentNotifications = recentNotifications.Take(5).Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    RelatedTicketId = n.RelatedTicketId,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    RelatedTicketTitle = null // TODO: チケットタイトルを取得する実装が必要
                }).ToList()
            };

            return ApiResponseDto<NotificationSummaryDto>.SuccessResult(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification summary for user");
            return StatusCode(500, ApiResponseDto<NotificationSummaryDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 未読通知数を取得
    /// </summary>
    [HttpGet("unread/count")]
    public async Task<ActionResult<ApiResponseDto<int>>> GetUnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.GetUnreadCountAsync(userId);

            return ApiResponseDto<int>.SuccessResult(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user");
            return StatusCode(500, ApiResponseDto<int>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 通知を既読にする
    /// </summary>
    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult<ApiResponseDto<string>>> MarkAsRead(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // 通知がユーザーのものかチェック
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            if (!notifications.Any(n => n.Id == id))
            {
                return Forbid();
            }

            await _notificationService.MarkAsReadAsync(id);

            return ApiResponseDto<string>.SuccessResult("success", "Notification marked as read");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read {NotificationId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// すべての通知を既読にする
    /// </summary>
    [HttpPut("read/all")]
    public async Task<ActionResult<ApiResponseDto<string>>> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _notificationService.MarkAllAsReadAsync(userId);

            return ApiResponseDto<string>.SuccessResult("success", "All notifications marked as read");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user");
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// テスト用通知を作成（開発環境のみ）
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<ApiResponseDto<NotificationDto>>> CreateTestNotification(
        [FromBody] CreateTestNotificationDto dto)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var userId = GetCurrentUserId();
            
            var notification = await _notificationService.CreateNotificationAsync(
                userId,
                dto.Title ?? "Test Notification",
                dto.Message ?? "This is a test notification",
                dto.Type ?? NotificationType.TicketAssigned,
                dto.RelatedTicketId);

            var notificationDto = new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                RelatedTicketId = notification.RelatedTicketId,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                RelatedTicketTitle = null // TODO: チケットタイトルを取得する実装が必要
            };

            return ApiResponseDto<NotificationDto>.SuccessResult(notificationDto, "Test notification created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test notification");
            return StatusCode(500, ApiResponseDto<NotificationDto>.ErrorResult("Internal server error"));
        }
    }
}

// 開発環境用のテスト通知作成DTO
public class CreateTestNotificationDto
{
    public string? Title { get; set; }
    public string? Message { get; set; }
    public NotificationType? Type { get; set; }
    public Guid? RelatedTicketId { get; set; }
}