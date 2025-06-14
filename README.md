# タスクベースチケット管理システム設計書

## 1. システム概要

### 1.1 目的
プロジェクトベースでタスクチケットを管理し、チーム内でのタスク進捗管理とコミュニケーションを効率化するWebアプリケーション。

### 1.2 技術スタック
- **フレームワーク**: .NET Aspire 9.3.0
- **言語**: C# (.NET 9.0)
- **データベース**: SQL Server
- **認証**: Keycloak
- **キャッシュ**: Redis
- **フロントエンド**: Blazor Server
- **デプロイ環境**: Ubuntu Server (オンプレミス)
- **テストフレームワーク**: NUnit, Aspire Testing

### 1.3 アーキテクチャ概要
このシステムはClean Architectureの原則に基づき、Organization-based多テナント権限管理システムを採用しています。

- **Organization**: 組織単位での権限管理とプロジェクト管理
- **階層的権限モデル**: Organization → Project → Ticket の3層構造
- **ロールベースアクセス制御**: 組織レベルとプロジェクトレベルでの細かな権限設定

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

### 3.1 ER図概要 (Organization-based権限管理)
```
User (Keycloakで管理)
  ↓
Organization ←→ OrganizationMember (OrganizationRole)
  ↓
Project ←→ ProjectMember (ProjectRole)
  ↓
Ticket ←→ TicketAssignment
  ↓        ↓
Comment   TicketHistory
  ↓
Notification
```

**権限階層**:
1. **Organization Level**: Admin, Manager, Member, Viewer
2. **Project Level**: Admin, Member, Viewer (Organization権限によって制限)
3. **Ticket Level**: Project権限を継承

### 3.2 エンティティ定義

#### Organization
```csharp
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Settings
    public int MaxProjects { get; set; } = 100;
    public int MaxMembers { get; set; } = 1000;
    public string? BillingPlan { get; set; }
    public DateTime? BillingExpiresAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<OrganizationMember> Members { get; set; }
    public virtual ICollection<Project> Projects { get; set; }
}
```

#### OrganizationMember
```csharp
public class OrganizationMember
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } // Keycloak UserId
    public string UserName { get; set; }
    public string? UserEmail { get; set; }
    public OrganizationRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? InvitedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastAccessedAt { get; set; }
    
    // Navigation properties
    public virtual Organization Organization { get; set; }
}

public enum OrganizationRole
{
    Viewer = 0,    // 閲覧のみ可能なメンバー
    Member = 1,    // プロジェクトに参加できる一般メンバー  
    Manager = 2,   // プロジェクトを作成・管理できるマネージャー
    Admin = 3      // Organization全体を管理できる管理者
}
```

#### Project
```csharp
public class Project
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }  // 所属する組織
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } // Keycloak UserId
    public bool IsActive { get; set; }
    
    // Navigation
    public Organization Organization { get; set; }
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

#### Organization管理
```
GET    /api/organizations                    # ユーザーが参加している組織一覧
POST   /api/organizations                    # 新規組織作成
GET    /api/organizations/{id}               # 組織詳細
PUT    /api/organizations/{id}               # 組織更新
DELETE /api/organizations/{id}               # 組織削除

GET    /api/organizations/{id}/members       # 組織メンバー一覧
POST   /api/organizations/{id}/members       # メンバー追加
PUT    /api/organizations/{id}/members/{userId}  # 権限変更
DELETE /api/organizations/{id}/members/{userId}  # メンバー削除

GET    /api/organizations/{id}/projects      # 組織内プロジェクト一覧
POST   /api/organizations/{id}/projects      # 組織内プロジェクト作成
```

#### プロジェクト管理
```
GET    /api/projects                 # ユーザーが参加しているプロジェクト一覧
POST   /api/projects                 # 新規プロジェクト作成（要OrganizationId）
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

## 5. Organization-based権限管理システム

### 5.1 権限モデル設計

#### 5.1.1 階層構造
```
Organization (組織)
├── Admin       # 組織全体の管理者権限
├── Manager     # プロジェクト作成・管理権限
├── Member      # 一般メンバー権限
└── Viewer      # 閲覧のみ権限

Project (プロジェクト) ※ Organization権限を継承
├── Admin       # プロジェクト管理者
├── Member      # チケット作成・編集可能
└── Viewer      # 閲覧のみ

Ticket (チケット) ※ Project権限を継承
├── 作成・編集・削除
├── コメント追加
└── ステータス変更
```

#### 5.1.2 権限マトリックス

| 操作 | Org Admin | Org Manager | Org Member | Org Viewer |
|------|-----------|-------------|------------|------------|
| 組織管理 | ✓ | ✗ | ✗ | ✗ |
| メンバー管理 | ✓ | ✗ | ✗ | ✗ |
| プロジェクト作成 | ✓ | ✓ | ✗ | ✗ |
| プロジェクト管理 | ✓ | ✓ | ✗* | ✗ |
| プロジェクト参加 | ✓ | ✓ | ✓ | ✓ |

*プロジェクトメンバーとして明示的に追加された場合のみ

### 5.2 実装アーキテクチャ

#### 5.2.1 サービス層構成
```csharp
// Organization管理サービス
public interface IOrganizationService
{
    Task<Organization> CreateOrganizationAsync(string name, string description, string createdBy);
    Task<OrganizationMember> AddMemberAsync(Guid organizationId, string userId, OrganizationRole role, string invitedBy);
    Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId);
    Task<bool> CanUserManageOrganizationAsync(Guid organizationId, string userId);
    // その他のメソッド...
}

// 権限チェック用サービス
public interface IPermissionService
{
    Task<bool> CanAccessOrganizationAsync(Guid organizationId, string userId);
    Task<bool> CanManageProjectAsync(Guid projectId, string userId);
    Task<bool> CanEditTicketAsync(Guid ticketId, string userId);
    // その他のメソッド...
}
```

#### 5.2.2 認可ポリシー設定
```csharp
// Program.cs での設定
builder.Services.AddAuthorization(options =>
{
    // Organization レベル
    options.AddPolicy("OrganizationAdmin", policy =>
        policy.Requirements.Add(new OrganizationRoleRequirement(OrganizationRole.Admin)));
        
    options.AddPolicy("OrganizationManager", policy =>
        policy.Requirements.Add(new OrganizationRoleRequirement(OrganizationRole.Manager)));
    
    // Project レベル
    options.AddPolicy("ProjectAdmin", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement(ProjectRole.Admin)));
        
    options.AddPolicy("ProjectMember", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement(ProjectRole.Member)));
});

// カスタム認可ハンドラー
public class OrganizationRoleHandler : AuthorizationHandler<OrganizationRoleRequirement>
{
    private readonly IOrganizationService _organizationService;
    
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationRoleRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var organizationId = context.Resource as Guid?;
        
        if (userId != null && organizationId.HasValue)
        {
            var userRole = await _organizationService.GetUserRoleAsync(organizationId.Value, userId);
            if (userRole >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }
        }
    }
}
```

#### 5.2.3 データ移行戦略
```csharp
// 既存データの組織移行
public class OrganizationMigrationService
{
    public async Task MigrateExistingDataAsync()
    {
        // 1. デフォルト組織の作成
        var defaultOrg = await CreateDefaultOrganizationAsync();
        
        // 2. 既存プロジェクトの組織への移行
        await MigrateProjectsToDefaultOrganizationAsync(defaultOrg.Id);
        
        // 3. プロジェクトメンバーの組織メンバーへの移行
        await MigrateProjectMembersToOrganizationAsync(defaultOrg.Id);
    }
    
    private async Task<Organization> CreateDefaultOrganizationAsync()
    {
        return await _organizationService.CreateOrganizationAsync(
            "Default Organization",
            "Default organization for migrated projects",
            "system"
        );
    }
}
```

### 5.3 パフォーマンス最適化

#### 5.3.1 キャッシング戦略
```csharp
// 組織・権限情報のキャッシング
public class CachedOrganizationService : IOrganizationService
{
    private readonly IOrganizationService _baseService;
    private readonly ICacheService _cache;
    
    public async Task<OrganizationRole?> GetUserRoleAsync(Guid organizationId, string userId)
    {
        var cacheKey = $"user-org-role:{userId}:{organizationId}";
        var cached = await _cache.GetAsync<OrganizationRole?>(cacheKey);
        
        if (cached == null)
        {
            cached = await _baseService.GetUserRoleAsync(organizationId, userId);
            await _cache.SetAsync(cacheKey, cached, TimeSpan.FromMinutes(15));
        }
        
        return cached;
    }
}
```

#### 5.3.2 データベースインデックス最適化
```sql
-- 組織関連のインデックス
CREATE INDEX IX_Organizations_Name ON Organizations(Name);
CREATE INDEX IX_Organizations_IsActive ON Organizations(IsActive);

-- 組織メンバーのインデックス
CREATE UNIQUE INDEX IX_OrganizationMembers_OrgId_UserId 
    ON OrganizationMembers(OrganizationId, UserId);
CREATE INDEX IX_OrganizationMembers_UserId ON OrganizationMembers(UserId);
CREATE INDEX IX_OrganizationMembers_IsActive ON OrganizationMembers(IsActive);

-- プロジェクト関連のインデックス
CREATE INDEX IX_Projects_OrganizationId ON Projects(OrganizationId);
CREATE INDEX IX_Projects_OrganizationId_IsActive 
    ON Projects(OrganizationId, IsActive);
```

### 5.4 セキュリティ考慮事項

#### 5.4.1 権限昇格防止
- 組織管理者のみが他ユーザーの権限を変更可能
- 最後の組織管理者の権限削除を禁止
- プロジェクト権限は組織権限を超えない制限

#### 5.4.2 データ分離
- 組織間のデータアクセス制御
- APIレベルでの組織IDバリデーション
- Cross-organization攻撃の防止

#### 5.4.3 監査ログ
```csharp
// 組織・権限変更の監査ログ
public class OrganizationAuditService
{
    public async Task LogMembershipChangeAsync(
        Guid organizationId,
        string targetUserId,
        OrganizationRole oldRole,
        OrganizationRole newRole,
        string changedBy)
    {
        var auditLog = new AuditLog
        {
            EntityType = "OrganizationMember",
            EntityId = organizationId.ToString(),
            Action = "RoleChanged",
            OldValue = oldRole.ToString(),
            NewValue = newRole.ToString(),
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Metadata = new { TargetUserId = targetUserId }
        };
        
        await _auditRepository.AddAsync(auditLog);
    }
}
```

## 6. フロントエンド設計

### 6.1 画面構成
1. **ログイン画面** (Keycloak連携)
2. **ダッシュボード**
   - 参加組織・プロジェクト一覧
   - 最近のアクティビティ
   - 自分に割り当てられたチケット

3. **組織管理**
   - 組織一覧・選択
   - 組織作成/編集
   - 組織メンバー管理
   - 組織設定（制限・請求等）

4. **プロジェクト管理**
   - プロジェクト一覧（組織内）
   - プロジェクト作成/編集
   - メンバー管理

5. **チケット管理**
   - チケット一覧（フィルタ・検索機能付き）
   - チケット詳細・編集
   - コメントスレッド表示

6. **レポート画面**
   - プロジェクト統計
   - バーンダウンチャート
   - チケット分析

7. **通知センター**
   - 通知一覧
   - リアルタイム通知（SignalR使用）

### 6.2 主要コンポーネント
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

## 11. Organization権限システム実装履歴

### 11.1 実装完了機能

#### 11.1.1 ドメインモデル（2024年12月実装）
- ✅ `Organization`エンティティ（組織管理）
  - 基本情報：名前、説明、作成日時
  - 制限設定：最大プロジェクト数、最大メンバー数
  - 課金情報：プラン、有効期限
- ✅ `OrganizationMember`エンティティ（組織メンバー管理）
  - ユーザー情報：UserId、UserName、Email
  - 権限管理：OrganizationRole（Admin, Manager, Member, Viewer）
  - アクティビティ：参加日時、最終アクセス日時
- ✅ `OrganizationRole`列挙型（階層権限定義）

#### 11.1.2 データアクセス層
- ✅ `OrganizationRepository`実装
- ✅ `OrganizationMemberRepository`実装
- ✅ Entity Framework設定・関係定義
- ✅ データベースマイグレーション（`20250613125340_AddOrganizationSupport`）
- ✅ 既存データ移行戦略（デフォルト組織への移行）

#### 11.1.3 ビジネスロジック層
- ✅ `OrganizationService`実装
  - 組織作成・更新・削除
  - メンバー管理（追加・削除・ロール変更）
  - 権限チェック（プロジェクト作成権限等）
  - 制限管理（プロジェクト数・メンバー数）
- ✅ キャッシング機能（Redis使用）
- ✅ 通知システム統合（組織イベント通知）

#### 11.1.4 API層
- ✅ `OrganizationsController`完全実装
  - CRUD操作（GET, POST, PUT, DELETE）
  - メンバー管理API
  - 組織内プロジェクト管理
  - 権限チェック統合
- ✅ 既存`ProjectsController`のOrganization対応
- ✅ エラーハンドリング・バリデーション
- ✅ APIドキュメント（Swagger）

#### 11.1.5 データ移行
- ✅ 非破壊的マイグレーション設計
  - デフォルト組織自動作成
  - 既存プロジェクトの組織への移行
  - システム管理者の自動作成
- ✅ 段階的移行戦略（nullable → non-nullable）

### 11.2 実装技術詳細

#### 11.2.1 パフォーマンス最適化
```csharp
// キャッシング戦略（15分間キャッシュ）
await _cacheService.SetAsync($"user-orgs:{userId}", orgList, TimeSpan.FromMinutes(15));

// 最適化されたクエリ（Include使用）
var organization = await _context.Organizations
    .Include(o => o.Members)
    .Include(o => o.Projects)
    .FirstOrDefaultAsync(o => o.Id == organizationId);
```

#### 11.2.2 セキュリティ実装
```csharp
// 権限チェック例
public async Task<bool> CanUserCreateProjectAsync(Guid organizationId, string userId)
{
    var role = await _memberRepository.GetUserRoleInOrganizationAsync(organizationId, userId);
    return role == OrganizationRole.Manager || role == OrganizationRole.Admin;
}

// 最後の管理者削除防止
if (member.Role == OrganizationRole.Admin && newRole != OrganizationRole.Admin)
{
    var adminCount = (await _memberRepository.GetOrganizationAdminsAsync(organizationId)).Count();
    if (adminCount <= 1)
        throw new InvalidOperationException("Cannot demote the last admin");
}
```

#### 11.2.3 データベース設計
```sql
-- 主要テーブル構造
CREATE TABLE Organizations (
    Id uniqueidentifier PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    DisplayName nvarchar(200),
    Description nvarchar(500),
    MaxProjects int NOT NULL DEFAULT 100,
    MaxMembers int NOT NULL DEFAULT 1000,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL,
    CreatedBy nvarchar(100) NOT NULL
);

-- 重要なインデックス
CREATE UNIQUE INDEX IX_Organizations_Name ON Organizations(Name);
CREATE UNIQUE INDEX IX_OrganizationMembers_OrgId_UserId 
    ON OrganizationMembers(OrganizationId, UserId);
```

### 11.3 今後の実装予定

#### 11.3.1 フロントエンド（優先度：高）
- [ ] 組織管理画面
  - 組織作成・編集フォーム
  - メンバー一覧・管理画面
  - 権限変更インターフェース
- [ ] ユーザー権限管理画面
  - ロール変更UI
  - 権限継承の可視化
- [ ] 組織選択・切り替え機能
  - 組織選択ドロップダウン
  - マルチ組織対応ナビゲーション

#### 11.3.2 高度な権限機能（優先度：中）
- [ ] カスタムロール定義
- [ ] 細粒度権限設定
- [ ] 権限テンプレート機能
- [ ] 一時的権限付与

#### 11.3.3 エンタープライズ機能（優先度：中）
- [ ] シングルサインオン（SSO）統合
- [ ] SCIM プロビジョニング
- [ ] 監査ログ詳細化
- [ ] コンプライアンスレポート

### 11.4 マイグレーション手順

#### 11.4.1 本番環境適用手順
```bash
# 1. バックアップ作成
backup database TicketDB to disk = 'TicketDB_backup.bak'

# 2. マイグレーション実行
dotnet ef database update --startup-project TicketManagement.ApiService

# 3. データ確認
SELECT COUNT(*) FROM Organizations;
SELECT COUNT(*) FROM OrganizationMembers;
```

#### 11.4.2 ロールバック手順
```bash
# マイグレーション前の状態に戻す
dotnet ef database update 20250613111747_UpdateTagsConfiguration
```

### 11.5 運用考慮事項

#### 11.5.1 監視項目
- 組織作成・削除の頻度
- メンバー数の増減トレンド
- 権限変更の監査
- API応答時間（権限チェック含む）

#### 11.5.2 制限事項・既知の問題
- 組織間のプロジェクト移動は未実装
- 大規模組織（1000+メンバー）での性能は未検証
- リアルタイム権限変更反映は要改善

このOrganization-based権限管理システムにより、マルチテナント対応の柔軟な権限管理が可能になりました。

## 12. Organization権限システム完全実装（2025年6月更新）

### 12.1 フロントエンド完全実装

#### 12.1.1 Organization管理画面（実装完了）
包括的な組織設定画面を実装し、管理者が組織の全側面を管理できるようになりました：

**OrganizationSettings.razor の主要機能：**
- **一般設定タブ**: 組織名、表示名、説明、アクティブ状態の管理
- **権限設定タブ**: デフォルトメンバーロール、プロジェクト作成権限、招待権限の設定
- **制限・使用量タブ**: 最大プロジェクト数・メンバー数の設定と現在の使用状況表示
- **通知設定タブ**: 電子メール・アプリ内通知の細かな制御
- **危険ゾーンタブ**: 組織のアーカイブ・削除機能（安全確認付き）

```razor
<!-- タブベースUI例 -->
<ul class="nav nav-tabs nav-fill mb-4">
    <li class="nav-item">
        <button class="nav-link @(activeTab == "general" ? "active" : "")" 
                @onclick='() => SetActiveTab("general")'>
            <span class="bi bi-sliders me-2"></span>General
        </button>
    </li>
    <!-- 他のタブ... -->
</ul>
```

#### 12.1.2 完全メンバー管理システム（実装完了）
高度なメンバー管理機能を実装：

**AddOrganizationMember.razor の機能：**
- **インテリジェント検索**: ユーザー名・メールでのリアルタイム検索
- **ロール選択**: Admin/Manager/Member/Viewerの詳細説明付き選択
- **デバウンス検索**: 500ms遅延で不要なAPI呼び出しを削減
- **検索結果表示**: ユーザー詳細情報と選択UI

**EditOrganizationMember.razor の機能：**
- **ロール変更**: 現在のロールから新しいロールへの安全な変更
- **権限説明**: 各ロールの詳細な権限説明
- **削除確認**: 完全な影響説明付きの削除プロセス
- **自己削除防止**: 現在のユーザーが自分を削除することを防止

**OrganizationMemberCard.razor の共通コンポーネント：**
```razor
<div class="card h-100">
    <div class="card-body">
        <h6 class="card-title">@(Member.UserName ?? Member.UserId)</h6>
        <span class="badge bg-@GetRoleBadgeColor(Member.Role)">
            @Member.Role.ToString()
        </span>
        <!-- アクション・統計情報 -->
    </div>
</div>
```

#### 12.1.3 Project作成のOrganization統合（実装完了）
Projectページを完全にOrganization統合に対応：

**Projects.razor の強化機能：**
- **組織フィルター**: ドロップダウンによる組織別プロジェクト表示
- **組織情報表示**: プロジェクトカードに組織名を表示（全組織表示時）
- **作成制限**: 組織選択時のみプロジェクト作成を許可
- **統計強化**: チケット数・メンバー数の表示
- **自動選択**: 単一組織メンバーの場合は自動選択

```csharp
// 組織フィルター処理
private void FilterProjectsByOrganization()
{
    if (selectedOrganization == null)
        projects = allProjects.ToList();
    else
        projects = allProjects
            .Where(p => p.OrganizationId == selectedOrganization.OrganizationId)
            .ToList();
}
```

#### 12.1.4 Dashboard Organization対応（実装完了）
ダッシュボードを組織別に最適化：

**Home.razor の新機能：**
- **組織選択ドロップダウン**: トップレベルでの組織切り替え
- **組織別統計**: 選択した組織のプロジェクト・チケット統計
- **リアルタイムフィルター**: 組織変更時の即座なデータ更新
- **組織情報表示**: プロジェクト一覧に組織名表示（全組織表示時）
- **自動読み込み**: 組織切り替え時のチケット統計再計算

### 12.2 API Client完全拡張（実装完了）

**TicketManagementApiClient.cs に追加された機能：**
- **組織API**: CRUD操作、メンバー管理、設定変更
- **ユーザー管理API**: 検索、詳細取得、組織情報取得
- **エラーハンドリング**: 包括的な例外処理とレスポンス検証

```csharp
// 組織メンバー管理API例
public async Task<ApiResponseDto<OrganizationMemberDto>?> 
    UpdateOrganizationMemberRoleAsync(Guid organizationId, string userId, 
                                     UpdateOrganizationMemberDto updateRole)
{
    var response = await _httpClient.PutAsJsonAsync(
        $"api/organizations/{organizationId}/members/{userId}/role", updateRole);
    // エラーハンドリング付き...
}
```

### 12.3 ナビゲーション統合（実装完了）

#### 12.3.1 MainLayout.razor の強化
- **OrganizationSelector**: 全ページで利用可能な組織切り替え機能
- **統一UI**: 一貫したナビゲーション体験
- **状態管理**: 選択した組織のローカルストレージ保存

#### 12.3.2 NavMenu.razor の更新
- **Organizationsリンク**: メインメニューに組織管理を追加
- **アイコン統一**: Bootstrap Iconsを使用した一貫したアイコン

### 12.4 現在の実装状況

#### 12.4.1 完了済み機能 ✅
- **バックエンド**: Organization エンティティ、サービス、API、認証・認可システム
- **データベース**: 安全なマイグレーション、インデックス最適化
- **テスト**: 包括的な単体テスト、統合テスト
- **フロントエンド**: 組織管理、メンバー管理、プロジェクト統合、ダッシュボード
- **ナビゲーション**: 組織選択器、メニュー統合
- **API**: 完全なRESTful API、エラーハンドリング

#### 12.4.2 実装状況詳細
```
✅ Organization管理画面（設定、メンバー、統計）
✅ Project作成のOrganization統合  
✅ Dashboard Organization対応
✅ ナビゲーション完全統合
✅ API Client完全拡張
✅ 権限チェック実装
✅ メンバー管理UI
✅ 組織切り替え機能
```

### 12.5 技術的成果

#### 12.5.1 アーキテクチャ改善
- **Clean Architecture**: レイヤー分離の完全実装
- **組織スコープ**: 全機能の組織レベル対応
- **パフォーマンス**: キャッシュ戦略、効率的なフィルター処理
- **セキュリティ**: 多層防御、ロールベースアクセス制御

#### 12.5.2 ユーザー体験向上
- **直感的UI**: タブベース設定、ドロップダウン選択
- **リアルタイム更新**: 即座のフィルター反映
- **エラーフレンドリー**: 詳細なエラーメッセージ、安全確認
- **レスポンシブ**: 全デバイス対応のデザイン

#### 12.5.3 開発者体験向上
- **再利用可能コンポーネント**: OrganizationMemberCard等
- **一貫したパターン**: API呼び出し、エラーハンドリング
- **包括的ログ**: デバッグ・監視対応
- **拡張性**: 新機能追加の容易さ

### 12.6 本番環境への適用

#### 12.6.1 デプロイメント準備
```bash
# 1. ビルド検証
dotnet build --configuration Release

# 2. テスト実行
dotnet test --configuration Release

# 3. データベース準備
dotnet ef database update --startup-project TicketManagement.ApiService
```

#### 12.6.2 機能検証チェックリスト
- [ ] 組織作成・編集・削除
- [ ] メンバー追加・権限変更・削除
- [ ] プロジェクト作成（組織指定）
- [ ] ダッシュボード組織切り替え
- [ ] 権限チェック動作
- [ ] パフォーマンス検証

### 12.7 今後の拡張可能性

#### 12.7.1 次期実装候補
- **組織間プロジェクト移動**: 所有権移転機能
- **高度な権限管理**: カスタムロール定義
- **監査ログ強化**: 詳細なアクティビティ追跡
- **通知システム拡張**: 組織レベル通知設定

#### 12.7.2 運用最適化
- **メトリクス**: 組織・プロジェクト使用状況分析
- **自動化**: メンバー管理の自動プロビジョニング
- **インテグレーション**: 外部システム連携強化

---

**🎉 Organization-based権限管理システム完全実装完了**

本システムは、エンタープライズレベルのマルチテナント対応を実現し、スケーラブルで安全、そして使いやすい組織管理機能を提供します。