using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.DTOs;
using TicketManagement.Contracts.Services;
using TicketManagement.Core.Enums;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly TicketDbContext _context;
    private readonly IProjectService _projectService;
    private readonly ITicketService _ticketService;

    public ReportService(
        TicketDbContext context,
        IProjectService projectService,
        ITicketService ticketService)
    {
        _context = context;
        _projectService = projectService;
        _ticketService = ticketService;
    }

    public async Task<ProjectSummaryReportDto> GetProjectSummaryAsync(Guid projectId)
    {
        var project = await _projectService.GetProjectAsync(projectId);
        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found");
        }

        var tickets = await _ticketService.GetTicketsByProjectAsync(projectId);
        var ticketsList = tickets.ToList();

        var summary = new ProjectSummaryReportDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            CreatedAt = project.CreatedAt,
            TotalMembers = project.Members.Count,
            TotalTickets = ticketsList.Count,
            OpenTickets = ticketsList.Count(t => t.Status == TicketStatus.Open),
            InProgressTickets = ticketsList.Count(t => t.Status == TicketStatus.InProgress),
            ClosedTickets = ticketsList.Count(t => t.Status == TicketStatus.Closed),
            TicketsByPriority = ticketsList
                .GroupBy(t => t.Priority)
                .ToDictionary(g => g.Key, g => g.Count()),
            CompletionRate = ticketsList.Any() 
                ? (double)ticketsList.Count(t => t.Status == TicketStatus.Closed) / ticketsList.Count * 100 
                : 0,
            LastActivityDate = ticketsList
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .FirstOrDefault()?.UpdatedAt ?? project.CreatedAt
        };

        return summary;
    }

    public async Task<BurndownChartDto> GetBurndownChartAsync(Guid projectId, ReportPeriodDto? period = null)
    {
        var effectivePeriod = period ?? new ReportPeriodDto { Days = 30 };
        var endDate = effectivePeriod.EndDate ?? DateTime.UtcNow.Date;
        var startDate = effectivePeriod.StartDate ?? endDate.AddDays(-effectivePeriod.Days);

        var tickets = await _context.Tickets
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Histories)
            .ToListAsync();

        var totalTickets = tickets.Count(t => t.CreatedAt <= endDate);
        var dataPoints = new List<BurndownDataPoint>();

        // 各日付のデータポイントを生成
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var remainingTickets = tickets.Count(t => 
                t.CreatedAt <= date && 
                (t.Histories == null || !t.Histories.Any(h => 
                    h.FieldName == "Status" && 
                    h.NewValue == TicketStatus.Closed.ToString() && 
                    h.ChangedAt <= date)));

            var completedTickets = totalTickets - remainingTickets;
            
            // 理想的な残りチケット数（線形減少）
            var daysTotal = (endDate - startDate).TotalDays;
            var daysPassed = (date - startDate).TotalDays;
            var idealRemaining = (int)(totalTickets * (1 - daysPassed / daysTotal));

            dataPoints.Add(new BurndownDataPoint
            {
                Date = date,
                RemainingTickets = remainingTickets,
                IdealRemainingTickets = Math.Max(0, idealRemaining),
                CompletedTickets = completedTickets
            });
        }

        return new BurndownChartDto
        {
            ProjectId = projectId,
            StartDate = startDate,
            EndDate = endDate,
            DataPoints = dataPoints,
            TotalTickets = totalTickets,
            RemainingTickets = dataPoints.LastOrDefault()?.RemainingTickets ?? 0
        };
    }

    public async Task<ProjectStatisticsDto> GetProjectStatisticsAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Include(p => p.Members)
            .Include(p => p.Tickets)
                .ThenInclude(t => t.Comments)
            .Include(p => p.Tickets)
                .ThenInclude(t => t.Assignments)
            .Include(p => p.Tickets)
                .ThenInclude(t => t.Histories)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new ArgumentException($"Project with ID {projectId} not found");
        }

        var now = DateTime.UtcNow;
        var oneWeekAgo = now.AddDays(-7);

        // チケット統計
        var ticketStats = new TicketStatistics
        {
            Total = project.Tickets.Count,
            ByStatus = project.Tickets
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByPriority = project.Tickets
                .GroupBy(t => t.Priority)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByCategory = project.Tickets
                .Where(t => !string.IsNullOrEmpty(t.Category))
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            OverdueTickets = project.Tickets
                .Count(t => t.DueDate.HasValue && t.DueDate < now && t.Status != TicketStatus.Closed),
            TicketsClosedThisWeek = project.Tickets
                .Count(t => t.Status == TicketStatus.Closed && 
                           t.Histories.Any(h => h.FieldName == "Status" && 
                                              h.NewValue == TicketStatus.Closed.ToString() && 
                                              h.ChangedAt >= oneWeekAgo)),
            TicketsCreatedThisWeek = project.Tickets
                .Count(t => t.CreatedAt >= oneWeekAgo)
        };

        // 平均解決時間を計算
        var closedTickets = project.Tickets
            .Where(t => t.Status == TicketStatus.Closed)
            .Select(t => new
            {
                CreatedAt = t.CreatedAt,
                ClosedAt = t.Histories
                    .Where(h => h.FieldName == "Status" && h.NewValue == TicketStatus.Closed.ToString())
                    .OrderBy(h => h.ChangedAt)
                    .FirstOrDefault()?.ChangedAt
            })
            .Where(t => t.ClosedAt.HasValue)
            .ToList();

        if (closedTickets.Any())
        {
            ticketStats.AverageResolutionTimeDays = closedTickets
                .Average(t => (t.ClosedAt!.Value - t.CreatedAt).TotalDays);
        }

        // メンバー統計
        var memberStats = new MemberStatistics
        {
            TotalMembers = project.Members.Count,
            ActiveMembers = project.Members.Count(m => 
                project.Tickets.Any(t => t.Assignments.Any(a => a.AssigneeId == m.UserId))),
            TicketsPerMember = project.Members
                .ToDictionary(
                    m => m.UserId,
                    m => project.Tickets.Count(t => t.Assignments.Any(a => a.AssigneeId == m.UserId))),
            MembersByRole = project.Members
                .GroupBy(m => m.Role)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        var mostActiveContributor = memberStats.TicketsPerMember
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();
        memberStats.MostActiveContributor = mostActiveContributor.Key ?? "";

        // 時間統計
        var timeStats = new TimeStatistics
        {
            ProjectStartDate = project.CreatedAt,
            DaysActive = (int)(now - project.CreatedAt).TotalDays,
            AverageTicketResolutionDays = ticketStats.AverageResolutionTimeDays,
            LastTicketCreatedDate = project.Tickets
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault()?.CreatedAt,
            LastTicketClosedDate = project.Tickets
                .Where(t => t.Status == TicketStatus.Closed)
                .SelectMany(t => t.Histories)
                .Where(h => h.FieldName == "Status" && h.NewValue == TicketStatus.Closed.ToString())
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefault()?.ChangedAt
        };

        // アクティビティ統計
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => now.Date.AddDays(-i))
            .OrderBy(d => d)
            .ToList();

        var activityStats = new ActivityStatistics
        {
            CommentsThisWeek = project.Tickets
                .SelectMany(t => t.Comments)
                .Count(c => c.CreatedAt >= oneWeekAgo),
            StatusChangesThisWeek = project.Tickets
                .SelectMany(t => t.Histories)
                .Count(h => h.FieldName == "Status" && h.ChangedAt >= oneWeekAgo),
            Last7DaysActivity = last7Days.Select(date => new DailyActivity
            {
                Date = date,
                TicketsCreated = project.Tickets.Count(t => t.CreatedAt.Date == date),
                TicketsClosed = project.Tickets
                    .SelectMany(t => t.Histories)
                    .Count(h => h.FieldName == "Status" && 
                               h.NewValue == TicketStatus.Closed.ToString() && 
                               h.ChangedAt.Date == date),
                Comments = project.Tickets
                    .SelectMany(t => t.Comments)
                    .Count(c => c.CreatedAt.Date == date),
                StatusChanges = project.Tickets
                    .SelectMany(t => t.Histories)
                    .Count(h => h.FieldName == "Status" && h.ChangedAt.Date == date)
            }).ToList()
        };

        // 今週のトップコントリビューター
        activityStats.TopContributorsThisWeek = project.Tickets
            .SelectMany(t => t.Comments.Where(c => c.CreatedAt >= oneWeekAgo))
            .GroupBy(c => c.AuthorId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ProjectStatisticsDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Tickets = ticketStats,
            Members = memberStats,
            Time = timeStats,
            Activity = activityStats
        };
    }

    public async Task<List<ProjectSummaryReportDto>> GetProjectsComparisonAsync(List<Guid> projectIds)
    {
        var summaries = new List<ProjectSummaryReportDto>();
        
        foreach (var projectId in projectIds)
        {
            try
            {
                var summary = await GetProjectSummaryAsync(projectId);
                summaries.Add(summary);
            }
            catch (ArgumentException)
            {
                // プロジェクトが見つからない場合はスキップ
                continue;
            }
        }

        return summaries.OrderByDescending(s => s.CompletionRate).ToList();
    }

    public async Task<Dictionary<string, object>> GetUserProductivityReportAsync(string userId, ReportPeriodDto? period = null)
    {
        var effectivePeriod = period ?? new ReportPeriodDto { Days = 30 };
        var endDate = effectivePeriod.EndDate ?? DateTime.UtcNow;
        var startDate = effectivePeriod.StartDate ?? endDate.AddDays(-effectivePeriod.Days);

        var userTickets = await _context.Tickets
            .Include(t => t.Assignments)
            .Include(t => t.Comments)
            .Include(t => t.Histories)
            .Where(t => t.Assignments.Any(a => a.AssigneeId == userId))
            .ToListAsync();

        var report = new Dictionary<string, object>
        {
            ["UserId"] = userId,
            ["Period"] = new { StartDate = startDate, EndDate = endDate },
            ["TotalAssignedTickets"] = userTickets.Count,
            ["CompletedTickets"] = userTickets.Count(t => 
                t.Status == TicketStatus.Closed &&
                t.Histories.Any(h => 
                    h.FieldName == "Status" && 
                    h.NewValue == TicketStatus.Closed.ToString() && 
                    h.ChangedAt >= startDate && 
                    h.ChangedAt <= endDate)),
            ["InProgressTickets"] = userTickets.Count(t => t.Status == TicketStatus.InProgress),
            ["CommentsWritten"] = await _context.Comments
                .CountAsync(c => c.AuthorId == userId && c.CreatedAt >= startDate && c.CreatedAt <= endDate),
            ["AverageResolutionDays"] = userTickets
                .Where(t => t.Status == TicketStatus.Closed)
                .Select(t => new
                {
                    AssignedAt = t.Assignments.First(a => a.AssigneeId == userId).AssignedAt,
                    ClosedAt = t.Histories
                        .Where(h => h.FieldName == "Status" && h.NewValue == TicketStatus.Closed.ToString())
                        .OrderBy(h => h.ChangedAt)
                        .FirstOrDefault()?.ChangedAt
                })
                .Where(t => t.ClosedAt.HasValue)
                .Select(t => (t.ClosedAt!.Value - t.AssignedAt).TotalDays)
                .DefaultIfEmpty(0)
                .Average()
        };

        return report;
    }
}