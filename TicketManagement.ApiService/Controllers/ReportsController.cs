using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;

namespace TicketManagement.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IProjectService _projectService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportService reportService,
        IProjectService projectService,
        ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _projectService = projectService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// プロジェクトのサマリーレポートを取得
    /// </summary>
    [HttpGet("projects/{projectId:guid}/summary")]
    public async Task<ActionResult<ApiResponseDto<ProjectSummaryReportDto>>> GetProjectSummary(Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // プロジェクトへのアクセス権限をチェック
            if (!await _projectService.CanUserAccessProjectAsync(projectId, userId))
            {
                return Forbid();
            }

            var summary = await _reportService.GetProjectSummaryAsync(projectId);
            return ApiResponseDto<ProjectSummaryReportDto>.SuccessResult(summary);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<ProjectSummaryReportDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project summary for {ProjectId}", projectId);
            return StatusCode(500, ApiResponseDto<ProjectSummaryReportDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// バーンダウンチャートデータを取得
    /// </summary>
    [HttpGet("projects/{projectId:guid}/burndown")]
    public async Task<ActionResult<ApiResponseDto<BurndownChartDto>>> GetBurndownChart(
        Guid projectId, 
        [FromQuery] ReportPeriodDto? period)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.CanUserAccessProjectAsync(projectId, userId))
            {
                return Forbid();
            }

            var burndown = await _reportService.GetBurndownChartAsync(projectId, period);
            return ApiResponseDto<BurndownChartDto>.SuccessResult(burndown);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<BurndownChartDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting burndown chart for {ProjectId}", projectId);
            return StatusCode(500, ApiResponseDto<BurndownChartDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// プロジェクトの詳細統計情報を取得
    /// </summary>
    [HttpGet("projects/{projectId:guid}/statistics")]
    public async Task<ActionResult<ApiResponseDto<ProjectStatisticsDto>>> GetProjectStatistics(Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.CanUserAccessProjectAsync(projectId, userId))
            {
                return Forbid();
            }

            var statistics = await _reportService.GetProjectStatisticsAsync(projectId);
            return ApiResponseDto<ProjectStatisticsDto>.SuccessResult(statistics);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponseDto<ProjectStatisticsDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project statistics for {ProjectId}", projectId);
            return StatusCode(500, ApiResponseDto<ProjectStatisticsDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 複数プロジェクトの比較レポートを取得
    /// </summary>
    [HttpPost("projects/comparison")]
    public async Task<ActionResult<ApiResponseDto<List<ProjectSummaryReportDto>>>> GetProjectsComparison(
        [FromBody] List<Guid> projectIds)
    {
        try
        {
            if (projectIds == null || !projectIds.Any())
            {
                return BadRequest(ApiResponseDto<List<ProjectSummaryReportDto>>.ErrorResult("Project IDs are required"));
            }

            var userId = GetCurrentUserId();
            
            // 各プロジェクトへのアクセス権限をチェック
            var accessibleProjects = new List<Guid>();
            foreach (var projectId in projectIds)
            {
                if (await _projectService.CanUserAccessProjectAsync(projectId, userId))
                {
                    accessibleProjects.Add(projectId);
                }
            }

            if (!accessibleProjects.Any())
            {
                return Forbid();
            }

            var comparison = await _reportService.GetProjectsComparisonAsync(accessibleProjects);
            return ApiResponseDto<List<ProjectSummaryReportDto>>.SuccessResult(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects comparison");
            return StatusCode(500, ApiResponseDto<List<ProjectSummaryReportDto>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 現在のユーザーの生産性レポートを取得
    /// </summary>
    [HttpGet("user/productivity")]
    public async Task<ActionResult<ApiResponseDto<Dictionary<string, object>>>> GetUserProductivity(
        [FromQuery] ReportPeriodDto? period)
    {
        try
        {
            var userId = GetCurrentUserId();
            var report = await _reportService.GetUserProductivityReportAsync(userId, period);
            return ApiResponseDto<Dictionary<string, object>>.SuccessResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user productivity report");
            return StatusCode(500, ApiResponseDto<Dictionary<string, object>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// 指定ユーザーの生産性レポートを取得（管理者のみ）
    /// </summary>
    [HttpGet("user/{targetUserId}/productivity")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponseDto<Dictionary<string, object>>>> GetUserProductivityByUserId(
        string targetUserId,
        [FromQuery] ReportPeriodDto? period)
    {
        try
        {
            var report = await _reportService.GetUserProductivityReportAsync(targetUserId, period);
            return ApiResponseDto<Dictionary<string, object>>.SuccessResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user productivity report for {UserId}", targetUserId);
            return StatusCode(500, ApiResponseDto<Dictionary<string, object>>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// ダッシュボード用の統合レポートを取得
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponseDto<Dictionary<string, object>>>> GetDashboardReport()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // ユーザーが参加しているプロジェクトを取得
            var userProjects = await _projectService.GetProjectsByUserAsync(userId);
            var projectIds = userProjects.Select(p => p.Id).Take(5).ToList(); // 最大5プロジェクト

            var dashboardData = new Dictionary<string, object>();

            // 各プロジェクトのサマリーを取得
            var projectSummaries = new List<ProjectSummaryReportDto>();
            foreach (var projectId in projectIds)
            {
                try
                {
                    var summary = await _reportService.GetProjectSummaryAsync(projectId);
                    projectSummaries.Add(summary);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get summary for project {ProjectId}", projectId);
                }
            }

            dashboardData["ProjectSummaries"] = projectSummaries;
            dashboardData["TotalProjects"] = projectSummaries.Count;
            dashboardData["TotalOpenTickets"] = projectSummaries.Sum(p => p.OpenTickets);
            dashboardData["TotalInProgressTickets"] = projectSummaries.Sum(p => p.InProgressTickets);
            dashboardData["AverageCompletionRate"] = projectSummaries.Any() 
                ? projectSummaries.Average(p => p.CompletionRate) 
                : 0;

            // ユーザーの生産性データ
            var userProductivity = await _reportService.GetUserProductivityReportAsync(userId, new ReportPeriodDto { Days = 7 });
            dashboardData["UserProductivity"] = userProductivity;

            return ApiResponseDto<Dictionary<string, object>>.SuccessResult(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard report");
            return StatusCode(500, ApiResponseDto<Dictionary<string, object>>.ErrorResult("Internal server error"));
        }
    }
}