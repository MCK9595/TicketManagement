# タスクベースチケット管理システム設計書

## 1. システム概要

### 1.1 目的
プロジェクトベースでタスクチケットを管理し、チーム内でのタスク進捗管理とコミュニケーションを効率化するWebアプリケーション。

### 1.2 技術スタック
- **フレームワーク**: .NET Aspire
- **言語**: C# (.NET 8)
- **データベース**: SQL Server
- **認証**: Keycloak
- **キャッシュ**: Redis
- **フロントエンド**: Blazor Server/WebAssembly
- **デプロイ環境**: Ubuntu Server (オンプレミス)
- **テストフレームワーク**: NUnit, Aspire Testing

## 1.3 プロジェクト作成手順

### 初期プロジェクト作成
```bash
# Aspireスターターテンプレートでプロジェクト作成
dotnet new aspire-starter --use-redis-cache --output TicketManagement

# ソリューションディレクトリに移動
cd TicketManagement
```

### 追加プロジェクトの作成
```bash
# Core（ドメインモデル）プロジェクト
dotnet new classlib -n TicketManagement.Core -o src/TicketManagement.Core
dotnet sln add src/TicketManagement.Core/TicketManagement.Core.csproj

# Infrastructure（データアクセス層）プロジェクト
dotnet new classlib -n TicketManagement.Infrastructure -o src/TicketManagement.Infrastructure
dotnet sln add src/TicketManagement.Infrastructure/TicketManagement.Infrastructure.csproj

# Contracts（共通インターフェース）プロジェクト
dotnet new classlib -n TicketManagement.Contracts -o src/TicketManagement.Contracts
dotnet sln add src/TicketManagement.Contracts/TicketManagement.Contracts.csproj

# 単体テストプロジェクト
dotnet new nunit -n TicketManagement.Tests -o tests/TicketManagement.Tests
dotnet sln add tests/TicketManagement.Tests/TicketManagement.Tests.csproj

# Aspire統合テストプロジェクト
dotnet new aspire-nunit -n TicketManagement.IntegrationTests -o tests/TicketManagement.IntegrationTests
dotnet sln add tests/TicketManagement.IntegrationTests/TicketManagement.IntegrationTests.csproj
```

## 2. システムアーキテクチャ

### 2.1 Aspireプロジェクト構成
```
TicketManagement/
├── TicketManagement.AppHost/          # Aspireオーケストレータ
├── TicketManagement.ServiceDefaults/  # 共通設定
├── TicketManagement.Web/              # Blazorフロントエンド
├── TicketManagement.ApiService/       # Web API
├── src/
│   ├── TicketManagement.Core/         # ドメインモデル・ビジネスロジック
│   ├── TicketManagement.Infrastructure/ # データアクセス層
│   └── TicketManagement.Contracts/    # 共通インターフェース
└── tests/
    ├── TicketManagement.Tests/        # 単体テスト
    └── TicketManagement.IntegrationTests/ # Aspire統合テスト
```

### 2.2 サービス構成
```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Redis Cache
var redis = builder.AddRedis("redis");

// SQL Server
var sqlServer = builder.AddSqlServer("sql")
    .WithDataVolume("sql-data");

var ticketDb = sqlServer.AddDatabase("TicketDB");

// Keycloak
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume("keycloak-data")
    .WithRealmImport("./keycloak-realm.json");

// API Service
var apiService = builder.AddProject<Projects.TicketManagement_ApiService>("apiservice")
    .WithReference(ticketDb)
    .WithReference(redis)
    .WithReference(keycloak);

// Web Frontend
builder.AddProject<Projects.TicketManagement_Web>("webfrontend")
    .WithReference(apiService)
    .WithReference(redis)
    .WithReference(keycloak);

builder.Build().Run();
```

## 3. データモデル設計

### 3.1 ER図概要
```
User (Keycloakで管理)
  ↓
Project ←→ ProjectMember
  ↓
Ticket ←→ TicketAssignment
  ↓        ↓
Comment   TicketHistory
  ↓
Notification
```

### 3.2 エンティティ定義

#### Project
```csharp
public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } // Keycloak UserId
    public bool IsActive { get; set; }
    
    // Navigation
    public ICollection<ProjectMember> Members { get; set; }
    public ICollection<Ticket> Tickets { get; set; }
}
```

#### ProjectMember
```csharp
public class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } // Keycloak UserId
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    
    // Navigation
    public Project Project { get; set; }
}

public enum ProjectRole
{
    Viewer = 0,      // 閲覧のみ
    Member = 1,      // 編集可能
    Admin = 2        // 管理者
}
```

#### Ticket
```csharp
public class Ticket
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public string Category { get; set; }
    public string[] Tags { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; }
    public DateTime? DueDate { get; set; }
    
    // Navigation
    public Project Project { get; set; }
    public ICollection<Comment> Comments { get; set; }
    public ICollection<TicketAssignment> Assignments { get; set; }
    public ICollection<TicketHistory> Histories { get; set; }
}

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Review = 2,
    Closed = 3,
    OnHold = 4
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
```

#### Comment
```csharp
public class Comment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Content { get; set; }
    public string AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; }
    
    // Navigation
    public Ticket Ticket { get; set; }
}
```

#### TicketAssignment
```csharp
public class TicketAssignment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string AssigneeId { get; set; }
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; }
    
    // Navigation
    public Ticket Ticket { get; set; }
}
```

#### TicketHistory
```csharp
public class TicketHistory
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string FieldName { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public HistoryActionType ActionType { get; set; }
    
    // Navigation
    public Ticket Ticket { get; set; }
}

public enum HistoryActionType
{
    Created = 0,
    Updated = 1,
    StatusChanged = 2,
    PriorityChanged = 3,
    Assigned = 4,
    Unassigned = 5,
    CommentAdded = 6
}
```

#### Notification
```csharp
public class Notification
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public Guid? RelatedTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public enum NotificationType
{
    TicketAssigned = 0,
    CommentAdded = 1,
    StatusChanged = 2,
    MentionedInComment = 3
}
```

## 4. API設計

### 4.1 認証・認可
```csharp
// Program.cs での設定
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://keycloak:8080/realms/ticket-management";
        options.Audience = "ticket-api";
        options.RequireHttpsMetadata = false; // 開発環境のみ
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProjectViewer", policy =>
        policy.RequireAuthenticatedUser());
    
    options.AddPolicy("ProjectMember", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("project_role", "Member", "Admin"));
    
    options.AddPolicy("ProjectAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("project_role", "Admin"));
});
```

### 4.2 主要エンドポイント

#### プロジェクト管理
```
GET    /api/projects                 # ユーザーが参加しているプロジェクト一覧
POST   /api/projects                 # 新規プロジェクト作成
GET    /api/projects/{id}            # プロジェクト詳細
PUT    /api/projects/{id}            # プロジェクト更新
DELETE /api/projects/{id}            # プロジェクト削除

GET    /api/projects/{id}/members    # メンバー一覧
POST   /api/projects/{id}/members    # メンバー追加
PUT    /api/projects/{id}/members/{userId}  # 権限変更
DELETE /api/projects/{id}/members/{userId}  # メンバー削除
```

#### チケット管理
```
GET    /api/projects/{projectId}/tickets           # チケット一覧（フィルタ・ページング対応）
POST   /api/projects/{projectId}/tickets           # チケット作成
GET    /api/tickets/{id}                          # チケット詳細
PUT    /api/tickets/{id}                          # チケット更新
DELETE /api/tickets/{id}                          # チケット削除

POST   /api/tickets/{id}/assign                   # 担当者アサイン
DELETE /api/tickets/{id}/assign/{assignmentId}    # アサイン解除
```

#### コメント管理
```
GET    /api/tickets/{ticketId}/comments    # コメント一覧
POST   /api/tickets/{ticketId}/comments    # コメント追加
PUT    /api/comments/{id}                  # コメント編集
DELETE /api/comments/{id}                  # コメント削除
```

#### 通知管理
```
GET    /api/notifications              # 通知一覧
PUT    /api/notifications/{id}/read    # 既読にする
PUT    /api/notifications/read-all     # 全て既読にする
```

#### レポート
```
GET    /api/projects/{id}/reports/summary     # プロジェクトサマリー
GET    /api/projects/{id}/reports/burndown    # バーンダウンチャート用データ
GET    /api/projects/{id}/reports/statistics  # 統計情報
```

## 5. フロントエンド設計

### 5.1 画面構成
1. **ログイン画面** (Keycloak連携)
2. **ダッシュボード**
   - 参加プロジェクト一覧
   - 最近のアクティビティ
   - 自分に割り当てられたチケット

3. **プロジェクト管理**
   - プロジェクト一覧
   - プロジェクト作成/編集
   - メンバー管理

4. **チケット管理**
   - チケット一覧（フィルタ・検索機能付き）
   - チケット詳細・編集
   - コメントスレッド表示

5. **レポート画面**
   - プロジェクト統計
   - バーンダウンチャート
   - チケット分析

6. **通知センター**
   - 通知一覧
   - リアルタイム通知（SignalR使用）

### 5.2 主要コンポーネント
```razor
<!-- TicketList.razor -->
@page "/projects/{ProjectId:guid}/tickets"
@attribute [Authorize(Policy = "ProjectViewer")]

<div class="ticket-filters">
    <TicketFilter OnFilterChanged="@HandleFilterChange" />
</div>

<div class="ticket-list">
    @foreach (var ticket in FilteredTickets)
    {
        <TicketCard Ticket="@ticket" />
    }
</div>

<Pagination CurrentPage="@currentPage" 
            TotalPages="@totalPages" 
            OnPageChanged="@HandlePageChange" />
```

## 6. 主要機能の実装詳細

### 6.1 リアルタイム通知（SignalR）
```csharp
// NotificationHub.cs
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.FindFirst("sub")?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
    
    public async Task SendNotificationToUser(string userId, Notification notification)
    {
        await Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", notification);
    }
}
```

### 6.2 検索・フィルタリング
```csharp
// TicketSearchService.cs
public class TicketSearchService
{
    public async Task<PagedResult<Ticket>> SearchTicketsAsync(
        Guid projectId, 
        TicketSearchCriteria criteria,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Tickets
            .Include(t => t.Assignments)
            .Include(t => t.Comments)
            .Where(t => t.ProjectId == projectId);
        
        // ステータスフィルタ
        if (criteria.Statuses?.Any() == true)
            query = query.Where(t => criteria.Statuses.Contains(t.Status));
        
        // 優先度フィルタ
        if (criteria.Priorities?.Any() == true)
            query = query.Where(t => criteria.Priorities.Contains(t.Priority));
        
        // キーワード検索
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            query = query.Where(t => 
                t.Title.Contains(criteria.Keyword) || 
                t.Description.Contains(criteria.Keyword));
        }
        
        // タグフィルタ
        if (criteria.Tags?.Any() == true)
        {
            query = query.Where(t => 
                t.Tags.Any(tag => criteria.Tags.Contains(tag)));
        }
        
        // ページング
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return new PagedResult<Ticket>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
```

### 6.3 履歴管理
```csharp
// TicketHistoryService.cs
public class TicketHistoryService
{
    private readonly IDbContextFactory<TicketDbContext> _contextFactory;
    
    public async Task RecordChangeAsync<T>(
        Guid ticketId, 
        string userId,
        Expression<Func<Ticket, T>> propertyExpression,
        T oldValue,
        T newValue,
        HistoryActionType actionType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var propertyName = GetPropertyName(propertyExpression);
        
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow,
            FieldName = propertyName,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            ActionType = actionType
        };
        
        context.TicketHistories.Add(history);
        await context.SaveChangesAsync();
    }
}
```

## 7. セキュリティ対策

### 7.1 認証・認可
- Keycloakによる統合認証
- JWTトークンベースの認証
- ロールベースアクセス制御（RBAC）
- プロジェクトレベルでの権限管理

### 7.2 データ保護
- SQL Injection対策（Entity Framework Core使用）
- XSS対策（Blazorの自動エスケープ）
- CSRF対策（AntiForgeryToken）
- 通信の暗号化（HTTPS必須）

### 7.3 監査ログ
- 全ての変更操作のログ記録
- ユーザーアクティビティの追跡
- 不正アクセスの検知

## 8. パフォーマンス最適化

### 8.1 データベース
- 適切なインデックスの設定
- N+1問題の回避（Include使用）
- 非同期処理の活用
- コネクションプーリング

### 8.2 キャッシング
- Redisによる分散キャッシング（Aspireで管理）
- 静的データのキャッシュ
- APIレスポンスのキャッシュ
- セッション管理

### 8.3 フロントエンド
- 遅延読み込み
- 仮想スクロール（大量データ表示時）
- 画像の最適化

## 9. テスト設計

### 9.1 単体テスト

#### 9.1.1 テスト対象と戦略
- **ビジネスロジック**: Core層のドメインモデルとサービス
- **データアクセス**: Infrastructureのリポジトリ（InMemory DB使用）
- **API エンドポイント**: コントローラーのロジック（モック使用）
- **カバレッジ目標**: 80%以上

#### 9.1.2 Core層の単体テスト例
```csharp
// tests/TicketManagement.Tests/Core/TicketTests.cs
using NUnit.Framework;
using TicketManagement.Core.Entities;
using TicketManagement.Core.Enums;

namespace TicketManagement.Tests.Core;

[TestFixture]
public class TicketTests
{
    private Ticket _ticket;

    [SetUp]
    public void Setup()
    {
        _ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user"
        };
    }

    [Test]
    public void Ticket_ShouldBeCreatedWithDefaultStatus()
    {
        // Arrange & Act
        var ticket = new Ticket();

        // Assert
        Assert.That(ticket.Status, Is.EqualTo(TicketStatus.Open));
    }

    [Test]
    public void Ticket_ShouldUpdateStatusCorrectly()
    {
        // Arrange
        var newStatus = TicketStatus.InProgress;

        // Act
        _ticket.UpdateStatus(newStatus, "updater-user");

        // Assert
        Assert.That(_ticket.Status, Is.EqualTo(newStatus));
        Assert.That(_ticket.UpdatedBy, Is.EqualTo("updater-user"));
        Assert.That(_ticket.UpdatedAt, Is.Not.Null);
    }

    [TestCase(TicketStatus.Open, TicketStatus.InProgress, true)]
    [TestCase(TicketStatus.Closed, TicketStatus.Open, false)]
    [TestCase(TicketStatus.InProgress, TicketStatus.Review, true)]
    public void Ticket_ShouldValidateStatusTransition(
        TicketStatus from, 
        TicketStatus to, 
        bool expectedValid)
    {
        // Arrange
        _ticket.Status = from;

        // Act
        var isValid = _ticket.CanTransitionTo(to);

        // Assert
        Assert.That(isValid, Is.EqualTo(expectedValid));
    }

    [Test]
    public void Ticket_ShouldAddCommentSuccessfully()
    {
        // Arrange
        var comment = new Comment
        {
            Content = "Test comment",
            AuthorId = "test-author",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _ticket.AddComment(comment);

        // Assert
        Assert.That(_ticket.Comments.Count, Is.EqualTo(1));
        Assert.That(_ticket.Comments.First().Content, Is.EqualTo("Test comment"));
    }
}

// tests/TicketManagement.Tests/Core/Services/TicketServiceTests.cs
[TestFixture]
public class TicketServiceTests
{
    private Mock<ITicketRepository> _ticketRepositoryMock;
    private Mock<INotificationService> _notificationServiceMock;
    private Mock<IHistoryService> _historyServiceMock;
    private TicketService _ticketService;

    [SetUp]
    public void Setup()
    {
        _ticketRepositoryMock = new Mock<ITicketRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _historyServiceMock = new Mock<IHistoryService>();
        
        _ticketService = new TicketService(
            _ticketRepositoryMock.Object,
            _notificationServiceMock.Object,
            _historyServiceMock.Object
        );
    }

    [Test]
    public async Task AssignTicket_ShouldNotifyAssignee()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var assigneeId = "assignee-123";
        var assignerId = "assigner-456";
        
        var ticket = new Ticket { Id = ticketId, Title = "Test Ticket" };
        _ticketRepositoryMock
            .Setup(r => r.GetByIdAsync(ticketId))
            .ReturnsAsync(ticket);

        // Act
        await _ticketService.AssignTicketAsync(ticketId, assigneeId, assignerId);

        // Assert
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                assigneeId,
                It.Is<Notification>(notif => 
                    notif.Type == NotificationType.TicketAssigned &&
                    notif.RelatedTicketId == ticketId
                )
            ),
            Times.Once
        );
        
        _historyServiceMock.Verify(
            h => h.RecordChangeAsync(
                ticketId,
                assignerId,
                It.IsAny<string>(),
                null,
                assigneeId,
                HistoryActionType.Assigned
            ),
            Times.Once
        );
    }
}
```

#### 9.1.3 Infrastructure層の単体テスト例
```csharp
// tests/TicketManagement.Tests/Infrastructure/TicketRepositoryTests.cs
[TestFixture]
public class TicketRepositoryTests
{
    private TicketDbContext _context;
    private TicketRepository _repository;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TicketDbContext(options);
        _repository = new TicketRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task GetByProjectIdAsync_ShouldReturnFilteredTickets()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var tickets = new List<Ticket>
        {
            new() { ProjectId = projectId, Title = "Ticket 1" },
            new() { ProjectId = projectId, Title = "Ticket 2" },
            new() { ProjectId = Guid.NewGuid(), Title = "Other Project Ticket" }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByProjectIdAsync(projectId);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.All(t => t.ProjectId == projectId), Is.True);
    }

    [Test]
    public async Task SearchAsync_ShouldFilterByMultipleCriteria()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var tickets = new List<Ticket>
        {
            new() 
            { 
                ProjectId = projectId, 
                Title = "Bug Fix", 
                Status = TicketStatus.Open,
                Priority = TicketPriority.High,
                Tags = new[] { "bug", "critical" }
            },
            new() 
            { 
                ProjectId = projectId, 
                Title = "Feature Request", 
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.Medium,
                Tags = new[] { "feature" }
            }
        };

        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        var criteria = new TicketSearchCriteria
        {
            Statuses = new[] { TicketStatus.Open },
            Priorities = new[] { TicketPriority.High },
            Tags = new[] { "bug" }
        };

        // Act
        var result = await _repository.SearchAsync(projectId, criteria);

        // Assert
        Assert.That(result.Items.Count, Is.EqualTo(1));
        Assert.That(result.Items.First().Title, Is.EqualTo("Bug Fix"));
    }
}
```

#### 9.1.4 API層の単体テスト例
```csharp
// tests/TicketManagement.Tests/Api/Controllers/TicketsControllerTests.cs
[TestFixture]
public class TicketsControllerTests
{
    private Mock<ITicketService> _ticketServiceMock;
    private Mock<IAuthorizationService> _authServiceMock;
    private TicketsController _controller;
    private ClaimsPrincipal _user;

    [SetUp]
    public void Setup()
    {
        _ticketServiceMock = new Mock<ITicketService>();
        _authServiceMock = new Mock<IAuthorizationService>();
        
        _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test-user-id"),
            new Claim("project_role", "Member")
        }));

        _controller = new TicketsController(
            _ticketServiceMock.Object,
            _authServiceMock.Object
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = _user }
            }
        };
    }

    [Test]
    public async Task CreateTicket_ShouldReturnCreatedResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var createDto = new CreateTicketDto
        {
            Title = "New Ticket",
            Description = "Description",
            Priority = TicketPriority.Medium
        };

        var createdTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = createDto.Title
        };

        _authServiceMock
            .Setup(a => a.AuthorizeAsync(
                _user, 
                projectId, 
                "ProjectMember"))
            .ReturnsAsync(AuthorizationResult.Success());

        _ticketServiceMock
            .Setup(s => s.CreateTicketAsync(projectId, createDto, "test-user-id"))
            .ReturnsAsync(createdTicket);

        // Act
        var result = await _controller.CreateTicket(projectId, createDto);

        // Assert
        Assert.That(result, Is.InstanceOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult.Value, Is.EqualTo(createdTicket));
    }

    [Test]
    public async Task GetTickets_WithUnauthorizedUser_ShouldReturnForbidden()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        _authServiceMock
            .Setup(a => a.AuthorizeAsync(
                _user, 
                projectId, 
                "ProjectViewer"))
            .ReturnsAsync(AuthorizationResult.Failed());

        // Act
        var result = await _controller.GetTickets(projectId);

        // Assert
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }
}
```

### 9.2 統合テスト (Aspire Testing)

#### 9.2.1 統合テスト構成
```csharp
// tests/TicketManagement.IntegrationTests/IntegrationTestBase.cs
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected DistributedApplication App { get; private set; }
    protected HttpClient ApiClient { get; private set; }
    protected HttpClient WebClient { get; private set; }

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TicketManagement_AppHost>();
        
        // テスト用の設定を追加
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        App = await appHost.BuildAsync();
        await App.StartAsync();

        // サービスのHTTPクライアントを取得
        ApiClient = App.CreateHttpClient("apiservice");
        WebClient = App.CreateHttpClient("webfrontend");
    }

    public async Task DisposeAsync()
    {
        if (App != null)
        {
            await App.DisposeAsync();
        }
    }

    protected async Task<string> GetTestUserTokenAsync()
    {
        // Keycloakからテストユーザーのトークンを取得
        var keycloakClient = App.CreateHttpClient("keycloak");
        var tokenResponse = await keycloakClient.PostAsJsonAsync(
            "/realms/ticket-management/protocol/openid-connect/token",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", "test-client"),
                new KeyValuePair<string, string>("username", "testuser"),
                new KeyValuePair<string, string>("password", "testpass")
            }));

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenData.AccessToken;
    }
}

// tests/TicketManagement.IntegrationTests/TicketApiIntegrationTests.cs
[TestFixture]
public class TicketApiIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task CreateAndRetrieveTicket_FullFlow()
    {
        // Arrange
        var token = await GetTestUserTokenAsync();
        ApiClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Step 1: Create a project
        var projectRequest = new CreateProjectDto
        {
            Name = "Integration Test Project",
            Description = "Test project for integration tests"
        };

        var projectResponse = await ApiClient.PostAsJsonAsync(
            "/api/projects", projectRequest);
        
        Assert.That(projectResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var project = await projectResponse.Content
            .ReadFromJsonAsync<ProjectDto>();

        // Step 2: Create a ticket
        var ticketRequest = new CreateTicketDto
        {
            Title = "Integration Test Ticket",
            Description = "This is a test ticket",
            Priority = TicketPriority.High,
            Category = "Bug",
            Tags = new[] { "test", "integration" }
        };

        var ticketResponse = await ApiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/tickets", ticketRequest);
        
        Assert.That(ticketResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var ticket = await ticketResponse.Content
            .ReadFromJsonAsync<TicketDto>();

        // Step 3: Add a comment
        var commentRequest = new CreateCommentDto
        {
            Content = "This is a test comment"
        };

        var commentResponse = await ApiClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments", commentRequest);
        
        Assert.That(commentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Step 4: Retrieve ticket with comments
        var getResponse = await ApiClient.GetAsync(
            $"/api/tickets/{ticket.Id}");
        
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var retrievedTicket = await getResponse.Content
            .ReadFromJsonAsync<TicketDetailDto>();

        // Assert
        Assert.That(retrievedTicket.Title, Is.EqualTo(ticketRequest.Title));
        Assert.That(retrievedTicket.Comments.Count, Is.EqualTo(1));
        Assert.That(retrievedTicket.Comments[0].Content, 
            Is.EqualTo(commentRequest.Content));
    }

    [Test]
    public async Task SearchTickets_WithFilters_ReturnsCorrectResults()
    {
        // Arrange
        var token = await GetTestUserTokenAsync();
        ApiClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateTestProjectAsync();
        
        // Create multiple tickets with different properties
        var tickets = new[]
        {
            new CreateTicketDto 
            { 
                Title = "High Priority Bug",
                Priority = TicketPriority.High,
                Status = TicketStatus.Open,
                Tags = new[] { "bug" }
            },
            new CreateTicketDto 
            { 
                Title = "Low Priority Feature",
                Priority = TicketPriority.Low,
                Status = TicketStatus.InProgress,
                Tags = new[] { "feature" }
            },
            new CreateTicketDto 
            { 
                Title = "Medium Priority Bug",
                Priority = TicketPriority.Medium,
                Status = TicketStatus.Open,
                Tags = new[] { "bug" }
            }
        };

        foreach (var ticket in tickets)
        {
            await ApiClient.PostAsJsonAsync(
                $"/api/projects/{projectId}/tickets", ticket);
        }

        // Act - Search for high priority bugs
        var searchResponse = await ApiClient.GetAsync(
            $"/api/projects/{projectId}/tickets?" +
            "priorities=High&tags=bug&statuses=Open");

        // Assert
        Assert.That(searchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var results = await searchResponse.Content
            .ReadFromJsonAsync<PagedResult<TicketDto>>();
        
        Assert.That(results.TotalCount, Is.EqualTo(1));
        Assert.That(results.Items[0].Title, Is.EqualTo("High Priority Bug"));
    }

    private async Task<Guid> CreateTestProjectAsync()
    {
        var project = new CreateProjectDto
        {
            Name = $"Test Project {Guid.NewGuid()}",
            Description = "Test project"
        };

        var response = await ApiClient.PostAsJsonAsync("/api/projects", project);
        var created = await response.Content.ReadFromJsonAsync<ProjectDto>();
        return created.Id;
    }
}

// tests/TicketManagement.IntegrationTests/NotificationIntegrationTests.cs
[TestFixture]
public class NotificationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task TicketAssignment_ShouldTriggerRealtimeNotification()
    {
        // Arrange
        var assigneeToken = await GetTestUserTokenAsync("assignee");
        var assignerToken = await GetTestUserTokenAsync("assigner");
        
        // Connect to SignalR hub as assignee
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{ApiClient.BaseAddress}notificationHub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(assigneeToken);
            })
            .Build();

        var notificationReceived = new TaskCompletionSource<Notification>();
        
        hubConnection.On<Notification>("ReceiveNotification", notification =>
        {
            notificationReceived.SetResult(notification);
        });

        await hubConnection.StartAsync();

        // Act - Assign ticket as assigner
        ApiClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", assignerToken);

        var projectId = await CreateTestProjectAsync();
        var ticketId = await CreateTestTicketAsync(projectId);

        var assignResponse = await ApiClient.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/assign",
            new AssignTicketDto { AssigneeId = "assignee" });

        Assert.That(assignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert - Wait for notification
        var notification = await notificationReceived.Task
            .WaitAsync(TimeSpan.FromSeconds(5));
        
        Assert.That(notification.Type, Is.EqualTo(NotificationType.TicketAssigned));
        Assert.That(notification.RelatedTicketId, Is.EqualTo(ticketId));
        
        await hubConnection.DisposeAsync();
    }
}

// tests/TicketManagement.IntegrationTests/PerformanceTests.cs
[TestFixture]
public class PerformanceTests : IntegrationTestBase
{
    [Test]
    public async Task BulkTicketCreation_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var token = await GetTestUserTokenAsync();
        ApiClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateTestProjectAsync();
        var ticketCount = 100;

        // Act
        var sw = Stopwatch.StartNew();
        
        var tasks = Enumerable.Range(1, ticketCount).Select(i =>
            ApiClient.PostAsJsonAsync(
                $"/api/projects/{projectId}/tickets",
                new CreateTicketDto
                {
                    Title = $"Performance Test Ticket {i}",
                    Description = $"Description {i}",
                    Priority = (TicketPriority)(i % 4)
                })
        );

        var responses = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Assert.That(responses.All(r => r.IsSuccessStatusCode), Is.True);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000), 
            $"Creating {ticketCount} tickets took {sw.ElapsedMilliseconds}ms");

        // Verify all tickets were created
        var listResponse = await ApiClient.GetAsync(
            $"/api/projects/{projectId}/tickets?pageSize={ticketCount}");
        
        var tickets = await listResponse.Content
            .ReadFromJsonAsync<PagedResult<TicketDto>>();
        
        Assert.That(tickets.TotalCount, Is.EqualTo(ticketCount));
    }
}
```

### 9.3 テスト実行とCI/CD

#### 9.3.1 テスト実行コマンド
```bash
# 単体テストの実行
dotnet test tests/TicketManagement.Tests/TicketManagement.Tests.csproj

# 統合テストの実行（Aspireサービスが起動される）
dotnet test tests/TicketManagement.IntegrationTests/TicketManagement.IntegrationTests.csproj

# カバレッジレポート付きテスト実行
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# 全テストの実行
dotnet test TicketManagement.sln
```

#### 9.3.2 GitHub Actions CI/CD
```yaml
# .github/workflows/ci.yml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Install Aspire workload
      run: dotnet workload install aspire
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run unit tests
      run: dotnet test tests/TicketManagement.Tests/TicketManagement.Tests.csproj --no-build --verbosity normal
    
    - name: Run integration tests
      run: dotnet test tests/TicketManagement.IntegrationTests/TicketManagement.IntegrationTests.csproj --no-build --verbosity normal
    
    - name: Generate coverage report
      run: |
        dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory ./coverage
        dotnet tool install --global dotnet-reportgenerator-globaltool
        reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report -reporttypes:Html
    
    - name: Upload coverage reports
      uses: actions/upload-artifact@v3
      with:
        name: coverage-report
        path: coverage/report
```

## 10. デプロイメント (Aspireベース)

### 10.1 Aspire デプロイメント設定
```csharp
// AppHost/Program.cs (Production設定)
var builder = DistributedApplication.CreateBuilder(args);

// 環境変数による設定
var isProduction = builder.Environment.IsProduction();

// Redis
var redis = builder.AddRedis("redis")
    .WithDataVolume("redis-data")
    .PublishAsContainer();

// SQL Server
var sqlServer = builder.AddSqlServer("sql", password: builder.AddParameter("sql-password"))
    .WithDataVolume("sql-data")
    .PublishAsContainer();

// その他のサービス設定...

if (isProduction)
{
    // 本番環境用の設定
    redis.WithPersistence();
    sqlServer.WithHealthCheck();
}

builder.Build().Run();
```

### 10.2 デプロイメントコマンド
```bash
# Aspire デプロイメントマニフェストの生成
dotnet run --project TicketManagement.AppHost/TicketManagement.AppHost.csproj -- --publisher manifest --output-path ../aspire-manifest.json

# コンテナイメージのビルドとプッシュ
azd deploy

# または手動でのデプロイ
dotnet publish -c Release
```

### Phase 2
- ファイル添付機能の実装
- メール通知機能
- モバイルアプリ対応
- ガントチャート表示

### Phase 3
- AI による自動分類・提案
- 外部サービス連携（GitHub, Jira等）
- ワークフロー自動化
- カスタムフィールド対応