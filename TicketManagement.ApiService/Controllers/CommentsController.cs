using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(ITicketService ticketService, ILogger<CommentsController> logger)
    {
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
    /// チケットにコメントを追加
    /// </summary>
    [HttpPost("ticket/{ticketId:guid}")]
    public async Task<ActionResult<ApiResponseDto<CommentDto>>> AddComment(
        Guid ticketId, 
        [FromBody] CreateCommentDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<CommentDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            
            if (!await _ticketService.CanUserAccessTicketAsync(ticketId, userId))
            {
                return Forbid();
            }

            var comment = await _ticketService.AddCommentAsync(ticketId, dto.Content, userId);

            var commentDto = new CommentDto
            {
                Id = comment.Id,
                TicketId = comment.TicketId,
                Content = comment.Content,
                AuthorId = comment.AuthorId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsEdited = comment.UpdatedAt.HasValue,
                TicketTitle = comment.Ticket?.Title ?? ""
            };

            return ApiResponseDto<CommentDto>.SuccessResult(commentDto, "Comment added successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<CommentDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to ticket {TicketId}", ticketId);
            return StatusCode(500, ApiResponseDto<CommentDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// コメントを取得
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<CommentDto>>> GetComment(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // まずコメントを取得してチケットアクセス権をチェック
            var comment = await _ticketService.GetTicketAsync(id);
            if (comment == null)
            {
                return NotFound(ApiResponseDto<CommentDto>.ErrorResult("Comment not found"));
            }

            var targetComment = comment.Comments?.FirstOrDefault(c => c.Id == id);
            if (targetComment == null)
            {
                return NotFound(ApiResponseDto<CommentDto>.ErrorResult("Comment not found"));
            }

            if (!await _ticketService.CanUserAccessTicketAsync(targetComment.TicketId, userId))
            {
                return Forbid();
            }

            var commentDto = new CommentDto
            {
                Id = targetComment.Id,
                TicketId = targetComment.TicketId,
                Content = targetComment.Content,
                AuthorId = targetComment.AuthorId,
                CreatedAt = targetComment.CreatedAt,
                UpdatedAt = targetComment.UpdatedAt,
                IsEdited = targetComment.UpdatedAt.HasValue,
                TicketTitle = targetComment.Ticket?.Title ?? ""
            };

            return ApiResponseDto<CommentDto>.SuccessResult(commentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comment {CommentId}", id);
            return StatusCode(500, ApiResponseDto<CommentDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// コメントを更新
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<CommentDto>>> UpdateComment(
        Guid id, 
        [FromBody] UpdateCommentDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponseDto<CommentDto>.ErrorResult(errors));
            }

            var userId = GetCurrentUserId();
            var comment = await _ticketService.UpdateCommentAsync(id, dto.Content, userId);

            var commentDto = new CommentDto
            {
                Id = comment.Id,
                TicketId = comment.TicketId,
                Content = comment.Content,
                AuthorId = comment.AuthorId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsEdited = comment.UpdatedAt.HasValue,
                TicketTitle = comment.Ticket?.Title ?? ""
            };

            return ApiResponseDto<CommentDto>.SuccessResult(commentDto, "Comment updated successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<CommentDto>.ErrorResult(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment {CommentId}", id);
            return StatusCode(500, ApiResponseDto<CommentDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// コメントを削除
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteComment(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _ticketService.DeleteCommentAsync(id, userId);

            return ApiResponseDto<string>.SuccessResult("success", "Comment deleted successfully");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<string>.ErrorResult(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId}", id);
            return StatusCode(500, ApiResponseDto<string>.ErrorResult("Internal server error"));
        }
    }
}