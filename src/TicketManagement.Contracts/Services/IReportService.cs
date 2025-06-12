using TicketManagement.Contracts.DTOs;

namespace TicketManagement.Contracts.Services;

/// <summary>
/// レポート生成サービスのインターフェース
/// </summary>
public interface IReportService
{
    /// <summary>
    /// プロジェクトサマリーレポートを取得
    /// </summary>
    Task<ProjectSummaryReportDto> GetProjectSummaryAsync(Guid projectId);
    
    /// <summary>
    /// バーンダウンチャートデータを取得
    /// </summary>
    Task<BurndownChartDto> GetBurndownChartAsync(Guid projectId, ReportPeriodDto? period = null);
    
    /// <summary>
    /// プロジェクト統計情報を取得
    /// </summary>
    Task<ProjectStatisticsDto> GetProjectStatisticsAsync(Guid projectId);
    
    /// <summary>
    /// 複数プロジェクトの比較レポートを取得
    /// </summary>
    Task<List<ProjectSummaryReportDto>> GetProjectsComparisonAsync(List<Guid> projectIds);
    
    /// <summary>
    /// ユーザーの生産性レポートを取得
    /// </summary>
    Task<Dictionary<string, object>> GetUserProductivityReportAsync(string userId, ReportPeriodDto? period = null);
}