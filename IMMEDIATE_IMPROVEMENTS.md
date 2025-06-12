# 即座実行可能な構造改善

## 1. 最重要: Application層の作成

### 問題
```csharp
❌ 現在: Infrastructure/Services/ProjectService.cs
❌ 現在: Infrastructure/Services/TicketService.cs
// 業務ロジックがインフラ層に混在
```

### 解決策
```csharp
✅ 新構造:
src/TicketManagement.Application/
├── Services/
│   ├── ProjectService.cs      // ビジネスロジック
│   ├── TicketService.cs       // ビジネスロジック
│   └── NotificationService.cs
├── Interfaces/
│   ├── IProjectService.cs
│   ├── ITicketService.cs
│   └── ICacheService.cs      // ContractsからApplication/Interfacesへ
└── DTOs/                     // ContractsのDTOsをここに移動
    ├── ProjectDTOs.cs
    ├── TicketDTOs.cs
    └── ...
```

## 2. Infrastructure層のクリーンアップ

### 残すもの（データアクセス関心事のみ）
```
src/TicketManagement.Infrastructure/
├── Data/
│   ├── TicketDbContext.cs
│   ├── Configurations/
│   ├── Repositories/
│   └── Migrations/
├── Caching/
│   └── CacheService.cs       // 技術的関心事
└── External/
    └── SignalRNotificationService.cs
```

### 削除・移動するもの
```
❌ Infrastructure/Services/ProjectService.cs    → Application/Services/
❌ Infrastructure/Services/TicketService.cs     → Application/Services/
❌ Infrastructure/Services/NotificationService.cs → Application/Services/
```

## 3. 依存関係修正

### 現在の問題
```csharp
❌ ApiService.csproj:
<ProjectReference Include="../src/TicketManagement.Infrastructure/..." />
// APIが直接Infrastructureを参照している
```

### 修正後
```csharp
✅ ApiService.csproj:
<ProjectReference Include="../src/TicketManagement.Application/..." />
// APIはApplicationのみ参照

✅ Infrastructure.csproj:
<ProjectReference Include="../TicketManagement.Application/..." />
// InfrastructureがApplicationの実装を提供
```

## 4. プロジェクト配置の統一

### 現在の不統一な配置
```
❌ 現在:
TicketManagement/
├── TicketManagement.ApiService/    # ルートレベル
├── TicketManagement.Web/          # ルートレベル
└── src/
    ├── TicketManagement.Core/      # src内
    └── TicketManagement.Infrastructure/ # src内
```

### 推奨統一配置
```
✅ 推奨:
TicketManagement/
├── src/
│   ├── Core/
│   │   ├── TicketManagement.Domain/     # Coreから名前変更
│   │   └── TicketManagement.Application/ # 新規作成
│   ├── Infrastructure/
│   │   └── TicketManagement.Infrastructure/
│   └── Presentation/
│       ├── TicketManagement.Api/        # ApiServiceから名前変更
│       └── TicketManagement.Web/
├── hosting/
│   ├── TicketManagement.AppHost/
│   └── TicketManagement.ServiceDefaults/
└── tests/
    └── # 既存テスト構造は維持
```

## 5. 段階的実装手順

### Phase 1A: Application層作成（30分）
1. `src/TicketManagement.Application`プロジェクト作成
2. 必要なNuGetパッケージ追加
3. フォルダ構造作成

### Phase 1B: インターフェース移動（20分）
1. `Contracts/Services/I*.cs` → `Application/Interfaces/`
2. 名前空間の更新

### Phase 1C: 業務サービス移動（45分）
1. `Infrastructure/Services/ProjectService.cs` → `Application/Services/`
2. `Infrastructure/Services/TicketService.cs` → `Application/Services/`
3. 名前空間とusing文更新

### Phase 1D: 依存関係修正（30分）
1. ApiServiceのプロジェクト参照変更
2. DI登録の修正
3. ビルドエラー修正

### Phase 1E: テスト実行（15分）
1. `dotnet build`でビルド確認
2. `dotnet test`でテスト実行
3. 問題があれば修正

## 6. 即座実行可能な小改善

### A. 空のフォルダ削除
```bash
# 不要な入れ子フォルダ削除
rm -rf src/TicketManagement.Infrastructure/src/
```

### B. 命名規則統一
```
ApiService → Api
ServiceDefaults → Shared.ServiceDefaults
```

### C. テスト構造改善
```
tests/
├── TicketManagement.Tests/           # Unit tests
├── TicketManagement.IntegrationTests/ # Integration tests
└── TicketManagement.Tests.Common/    # Test utilities (新規)
```

## 7. 期待される効果（改善前後比較）

### ビルド時間改善
```
改善前: ApiService → Infrastructure → Core (不要な依存)
改善後: Api → Application → Domain (最小依存)
```

### テスト独立性
```
改善前: BusinessLogic + DataAccess が混在
改善後: Application層単独テスト可能
```

### 保守性向上
```
改善前: ビジネス変更でInfrastructure修正
改善後: Application層のみ修正
```

## 8. 移行リスク管理

### 低リスク改善（すぐ実行可能）
- Application層作成
- インターフェース移動
- 空フォルダ削除

### 中リスク改善（テスト必須）
- 業務サービス移動
- 依存関係変更

### 高リスク改善（慎重実装）
- プロジェクト名変更
- フォルダ大規模移動

## 9. 実装チェックリスト

### Application層作成
- [ ] プロジェクト作成
- [ ] NuGetパッケージ追加
- [ ] フォルダ構造作成
- [ ] インターフェース移動
- [ ] 業務サービス移動

### 依存関係修正
- [ ] ApiServiceの参照変更
- [ ] DI登録修正
- [ ] 名前空間更新
- [ ] ビルド確認
- [ ] テスト実行