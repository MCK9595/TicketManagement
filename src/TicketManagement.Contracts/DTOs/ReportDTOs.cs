using TicketManagement.Core.Enums;

namespace TicketManagement.Contracts.DTOs;

/// <summary>
/// プロジェクトサマリーレポート
/// </summary>
public class ProjectSummaryReportDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TotalMembers { get; set; }
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ClosedTickets { get; set; }
    public Dictionary<TicketPriority, int> TicketsByPriority { get; set; } = new();
    public double CompletionRate { get; set; }
    public DateTime? LastActivityDate { get; set; }
}

/// <summary>
/// バーンダウンチャートデータ
/// </summary>
public class BurndownChartDto
{
    public Guid ProjectId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BurndownDataPoint> DataPoints { get; set; } = new();
    public int TotalTickets { get; set; }
    public int RemainingTickets { get; set; }
}

public class BurndownDataPoint
{
    public DateTime Date { get; set; }
    public int RemainingTickets { get; set; }
    public int IdealRemainingTickets { get; set; }
    public int CompletedTickets { get; set; }
}

/// <summary>
/// プロジェクト統計情報
/// </summary>
public class ProjectStatisticsDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    
    // チケット統計
    public TicketStatistics Tickets { get; set; } = new();
    
    // メンバー統計
    public MemberStatistics Members { get; set; } = new();
    
    // 時間統計
    public TimeStatistics Time { get; set; } = new();
    
    // アクティビティ統計
    public ActivityStatistics Activity { get; set; } = new();
}

public class TicketStatistics
{
    public int Total { get; set; }
    public Dictionary<TicketStatus, int> ByStatus { get; set; } = new();
    public Dictionary<TicketPriority, int> ByPriority { get; set; } = new();
    public Dictionary<string, int> ByCategory { get; set; } = new();
    public double AverageResolutionTimeDays { get; set; }
    public int OverdueTickets { get; set; }
    public int TicketsClosedThisWeek { get; set; }
    public int TicketsCreatedThisWeek { get; set; }
}

public class MemberStatistics
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public Dictionary<string, int> TicketsPerMember { get; set; } = new();
    public string MostActiveContributor { get; set; } = string.Empty;
    public Dictionary<ProjectRole, int> MembersByRole { get; set; } = new();
}

public class TimeStatistics
{
    public DateTime ProjectStartDate { get; set; }
    public int DaysActive { get; set; }
    public double AverageTicketResolutionDays { get; set; }
    public double AverageTimeInProgressDays { get; set; }
    public DateTime? LastTicketCreatedDate { get; set; }
    public DateTime? LastTicketClosedDate { get; set; }
}

public class ActivityStatistics
{
    public int CommentsThisWeek { get; set; }
    public int StatusChangesThisWeek { get; set; }
    public List<DailyActivity> Last7DaysActivity { get; set; } = new();
    public Dictionary<string, int> TopContributorsThisWeek { get; set; } = new();
}

public class DailyActivity
{
    public DateTime Date { get; set; }
    public int TicketsCreated { get; set; }
    public int TicketsClosed { get; set; }
    public int Comments { get; set; }
    public int StatusChanges { get; set; }
}

/// <summary>
/// レポート期間指定用DTO
/// </summary>
public class ReportPeriodDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Days { get; set; } = 30; // デフォルト30日
}