# ログシステム統合実装例

## 実装完了内容

### ✅ 包括的ログシステムの構築

1. **構造化ログ基盤**
   - LogEvent モデル（セキュリティ、監査、パフォーマンス、ビジネス）
   - StructuredLogger サービス（PII保護、機密情報フィルタリング）
   - LogEnrichmentService（コンテキスト自動追加）

2. **専門ログサービス**
   - `AuditLogService`: プロジェクト・チケット・コメント操作の監査
   - `SecurityLogService`: 認証・認可・不正アクセス検知
   - `PerformanceLogService`: API・DB・キャッシュ・SignalRパフォーマンス

3. **ログミドルウェア**
   - `EnhancedLoggingMiddleware`: リクエスト・レスポンス・セキュリティチェック
   - `AuthenticationLoggingMiddleware`: 認証状態変化の追跡
   - `PerformanceMonitoringMiddleware`: システムリソース監視

4. **設定とDI**
   - 包括的ログ設定（本番・開発環境別）
   - DIコンテナ統合
   - ミドルウェアパイプライン設定

## サービス統合例

### ProjectService でのログ統合

```csharp
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly IAuditLogService _auditLogService;
    private readonly IPerformanceLogService _performanceLogService;

    public async Task<Project> CreateProjectAsync(string name, string description, string createdBy)
    {
        using var timer = _performanceLogService.MeasureBusinessProcessPerformance(
            "ProjectManagement", "CreateProject");

        try
        {
            // 入力検証
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name cannot be empty", nameof(name));

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                IsActive = true
            };

            var createdProject = await _projectRepository.AddAsync(project);
            
            // 監査ログ
            await _auditLogService.LogProjectCreatedAsync(createdProject);
            
            // キャッシュ無効化
            await _cacheService.RemoveAsync(CacheKeys.UserProjects(createdBy));

            return createdProject;
        }
        catch (Exception ex)
        {
            // エラーログは自動的にミドルウェアで記録される
            throw;
        }
    }
}
```

### TicketService でのログ統合

```csharp
public async Task<Ticket> UpdateTicketAsync(Guid ticketId, UpdateTicketDto updateDto, string updatedBy)
{
    using var timer = _performanceLogService.MeasureBusinessProcessPerformance(
        "TicketManagement", "UpdateTicket");

    var oldTicket = await _ticketRepository.GetByIdAsync(ticketId);
    if (oldTicket == null)
        throw new NotFoundException($"Ticket {ticketId} not found");

    // 変更前の値を保存
    var originalStatus = oldTicket.Status;
    
    // 更新実行
    oldTicket.Title = updateDto.Title;
    oldTicket.Description = updateDto.Description;
    oldTicket.Status = updateDto.Status;
    oldTicket.Priority = updateDto.Priority;
    oldTicket.UpdatedAt = DateTime.UtcNow;
    oldTicket.UpdatedBy = updatedBy;

    var updatedTicket = await _ticketRepository.UpdateAsync(oldTicket);

    // 監査ログ
    await _auditLogService.LogTicketUpdatedAsync(oldTicket, updatedTicket);

    // ステータス変更の場合は特別ログ
    if (originalStatus != updateDto.Status)
    {
        await _auditLogService.LogTicketStatusChangedAsync(
            updatedTicket, originalStatus.ToString(), updateDto.Status.ToString());
    }

    return updatedTicket;
}
```

### Controller でのログ統合

```csharp
[HttpPost]
public async Task<ActionResult<ProjectResponseDto>> CreateProject(CreateProjectDto createDto)
{
    using var timer = _performanceLogService.MeasureApiPerformance(
        "projects", "POST", 201);

    try
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await _securityLogService.LogUnauthorizedAccessAttemptAsync("projects", "create");
            return Unauthorized();
        }

        var project = await _projectService.CreateProjectAsync(
            createDto.Name, createDto.Description, userId);

        var response = new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            IsActive = project.IsActive
        };

        // ユーザーアクティビティログ
        await _auditLogService.LogDataAccessAsync("create", "Project", project.Id.ToString());

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, response);
    }
    catch (ValidationException ex)
    {
        await _securityLogService.LogInputValidationFailureAsync(
            "ProjectCreation", "ValidationFailure", ex.Message);
        return BadRequest(ex.Message);
    }
}
```

## ログ出力例

### セキュリティログ例
```json
{
  "timestamp": "2024-06-11T10:30:00.123Z",
  "level": "Warning",
  "message": "Security Event: LoginFailure",
  "userId": "unknown",
  "correlationId": "req-abc123",
  "ipAddress": "192.168.1.***",
  "securityEvent": {
    "eventType": "LoginFailure",
    "resource": "Authentication",
    "action": "Login",
    "success": false,
    "failureReason": "Invalid credentials",
    "metadata": {
      "authMethod": "JWT",
      "attemptTime": "2024-06-11T10:30:00.123Z"
    }
  }
}
```

### 監査ログ例
```json
{
  "timestamp": "2024-06-11T10:31:00.456Z",
  "level": "Information",
  "message": "Audit: DataCreate on Ticket",
  "userId": "user-123",
  "correlationId": "req-def456",
  "auditEvent": {
    "operation": "DataCreate",
    "entityType": "Ticket",
    "entityId": "ticket-789",
    "newValue": {
      "title": "New Feature Request",
      "status": "Open",
      "priority": "Medium",
      "projectId": "project-456"
    }
  }
}
```

### パフォーマンスログ例
```json
{
  "timestamp": "2024-06-11T10:32:00.789Z",
  "level": "Warning",
  "message": "Performance: POST /api/projects took 2150ms",
  "userId": "user-123",
  "correlationId": "req-ghi789",
  "performanceEvent": {
    "operation": "POST /api/projects",
    "durationMs": 2150,
    "category": "ApiRequest",
    "isSlowOperation": true,
    "metrics": {
      "endpoint": "/api/projects",
      "method": "POST",
      "statusCode": 201
    }
  }
}
```

## ログ集約・監視戦略

### 1. ログレベル戦略
- **Trace**: 詳細なデバッグ情報（開発環境のみ）
- **Debug**: 開発・テスト環境での詳細情報
- **Information**: 通常の業務ログ、監査ログ
- **Warning**: パフォーマンス問題、軽微なセキュリティイベント
- **Error**: エラー、重要なセキュリティイベント
- **Critical**: システム障害、データ侵害

### 2. アラート設定推奨
- **即座アラート**: 複数回ログイン失敗、不正アクセス試行、システムエラー
- **日次レポート**: パフォーマンス集計、利用統計
- **週次レポート**: セキュリティ動向、監査サマリー

### 3. ログ保持ポリシー
- **セキュリティログ**: 1年保持
- **監査ログ**: 法的要件に応じて（推奨: 3-7年）
- **パフォーマンスログ**: 3ヶ月保持
- **デバッグログ**: 1週間保持

### 4. 将来拡張
- **Elasticsearch** での集約・検索
- **Grafana** でのダッシュボード化
- **機械学習** による異常検知
- **SIEM** システムとの統合

## 実装のベストプラクティス

### 1. パフォーマンス考慮
- 非同期ログ記録（`await` の適切な使用）
- バックグラウンド処理でのログ送信
- ログレベルによる記録制御

### 2. セキュリティ考慮
- PII（個人情報）の自動マスキング
- 機密情報のフィルタリング
- ログアクセス制御

### 3. 可用性考慮
- ログ失敗時のアプリケーション継続
- ローカルフォールバック
- ログバッファリング

この包括的ログシステムにより、TicketManagementアプリケーションは企業グレードの監査・セキュリティ・パフォーマンス監視機能を持つことになります。