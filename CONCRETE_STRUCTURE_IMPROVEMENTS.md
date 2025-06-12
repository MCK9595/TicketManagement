# 具体的構造改善実装ガイド

## 現在の構造問題と解決策

### 問題1: 業務ロジックの配置ミス
```
❌ 現在: Infrastructure/Services/ に業務ロジック
├── ProjectService.cs       # ← ビジネスロジック
├── TicketService.cs        # ← ビジネスロジック  
├── NotificationService.cs  # ← ビジネスロジック
├── ReportService.cs        # ← ビジネスロジック
└── CacheService.cs         # ← インフラ関心事（適切）
```

### 解決策: Application層の作成
```
✅ 改善後の構造:
src/
├── TicketManagement.Application/     # 新規作成
│   ├── Services/                     # 業務ロジック
│   │   ├── ProjectService.cs
│   │   ├── TicketService.cs
│   │   ├── NotificationService.cs
│   │   └── ReportService.cs
│   ├── Interfaces/                   # アプリケーション層インターフェース
│   │   ├── IProjectService.cs
│   │   ├── ITicketService.cs
│   │   └── ICacheService.cs
│   ├── DTOs/                         # Contractsから移動
│   │   ├── ProjectDTOs.cs
│   │   ├── TicketDTOs.cs
│   │   └── CommonDTOs.cs
│   └── Validators/                   # 検証ロジック
│       └── ValidationAttributes.cs
│
├── TicketManagement.Infrastructure/  # データアクセスのみ
│   ├── Data/                        # EF Core関連
│   ├── Repositories/                # データアクセス実装
│   ├── Caching/                     # 技術的キャッシュ実装
│   │   └── CacheService.cs
│   └── External/                    # 外部サービス
│       └── SignalRNotificationService.cs
│
└── TicketManagement.Contracts/      # 削除または最小化
    └── Repositories/                # リポジトリインターフェースのみ
```

## 実装手順（段階的アプローチ）

### ステップ1: Application層作成 (15分)
```bash
# 1. プロジェクト作成
cd /home/macky/src/TicketManagement/src/
dotnet new classlib -n TicketManagement.Application

# 2. 必要パッケージ追加
cd TicketManagement.Application
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add package Microsoft.Extensions.Caching.Abstractions

# 3. プロジェクト参照追加
dotnet add reference ../TicketManagement.Core/TicketManagement.Core.csproj

# 4. フォルダ構造作成
mkdir Services Interfaces DTOs Validators
```

### ステップ2: インターフェース移動 (10分)
```bash
# Contracts/Services/ → Application/Interfaces/ へ移動
mv ../TicketManagement.Contracts/Services/*.cs ./Interfaces/

# 名前空間変更が必要：
# TicketManagement.Contracts.Services → TicketManagement.Application.Interfaces
```

### ステップ3: 業務サービス移動 (20分)
```bash
# Infrastructure/Services/ → Application/Services/ へ移動（CacheService以外）
cp ../TicketManagement.Infrastructure/Services/ProjectService.cs ./Services/
cp ../TicketManagement.Infrastructure/Services/TicketService.cs ./Services/
cp ../TicketManagement.Infrastructure/Services/NotificationService.cs ./Services/
cp ../TicketManagement.Infrastructure/Services/ReportService.cs ./Services/

# Infrastructure から削除（CacheService は残す）
# rm ../TicketManagement.Infrastructure/Services/ProjectService.cs
# rm ../TicketManagement.Infrastructure/Services/TicketService.cs
# rm ../TicketManagement.Infrastructure/Services/NotificationService.cs  
# rm ../TicketManagement.Infrastructure/Services/ReportService.cs
```

### ステップ4: 依存関係修正
```xml
<!-- ApiService.csproj 修正 -->
<ProjectReference Include="../src/TicketManagement.Application/TicketManagement.Application.csproj" />
<!-- Infrastructure参照を削除 -->

<!-- Infrastructure.csproj 修正 -->
<ProjectReference Include="../TicketManagement.Application/TicketManagement.Application.csproj" />
```

## コード修正例

### 1. Program.cs の DI登録修正
```csharp
// 修正前
builder.Services.AddScoped<IProjectService, ProjectService>();
// using TicketManagement.Infrastructure.Services;

// 修正後  
builder.Services.AddScoped<IProjectService, ProjectService>();
// using TicketManagement.Application.Services;
```

### 2. 名前空間修正
```csharp
// ProjectService.cs 修正
// 修正前
namespace TicketManagement.Infrastructure.Services;

// 修正後
namespace TicketManagement.Application.Services;

// using追加
using TicketManagement.Application.Interfaces;
```

### 3. コントローラー修正
```csharp
// ProjectsController.cs
// 修正前
using TicketManagement.Infrastructure.Services;

// 修正後  
using TicketManagement.Application.Services;
using TicketManagement.Application.Interfaces;
```

## 推奨フォルダ構成（完全版）

### 最終的な理想構造
```
TicketManagement/
├── src/
│   ├── Core/
│   │   ├── TicketManagement.Domain/           # Coreから名前変更
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   ├── ValueObjects/                  # 将来拡張
│   │   │   └── DomainEvents/                  # 将来拡張
│   │   └── TicketManagement.Application/      # 新規作成
│   │       ├── Services/                      # 業務サービス
│   │       ├── Interfaces/                    # アプリケーション層インターフェース
│   │       ├── DTOs/                          # データ転送オブジェクト
│   │       ├── Validators/                    # 入力検証
│   │       ├── Commands/                      # 将来: CQRS Command
│   │       ├── Queries/                       # 将来: CQRS Query
│   │       └── Behaviors/                     # 将来: パイプライン
│   │
│   ├── Infrastructure/
│   │   ├── TicketManagement.Infrastructure.Data/     # データアクセス専用
│   │   │   ├── Contexts/
│   │   │   ├── Configurations/
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   ├── TicketManagement.Infrastructure.Caching/  # キャッシュ専用
│   │   │   └── Services/
│   │   └── TicketManagement.Infrastructure.Messaging/ # SignalR等
│   │       ├── SignalR/
│   │       └── Services/
│   │
│   └── Presentation/
│       ├── TicketManagement.Api/              # ApiServiceから名前変更
│       │   ├── Controllers/
│       │   ├── Middleware/
│       │   ├── Filters/
│       │   └── Hubs/
│       └── TicketManagement.Web/
│           ├── Components/
│           ├── Services/
│           └── Authentication/
│
├── hosting/                                   # 新フォルダ
│   ├── TicketManagement.AppHost/
│   └── TicketManagement.ServiceDefaults/
│
└── tests/
    ├── Unit/                                  # 高速テスト
    │   ├── TicketManagement.Domain.Tests/
    │   ├── TicketManagement.Application.Tests/
    │   └── TicketManagement.Infrastructure.Tests/
    ├── Integration/                           # 統合テスト
    │   ├── TicketManagement.Api.IntegrationTests/
    │   └── TicketManagement.Infrastructure.IntegrationTests/
    └── Common/                                # テストユーティリティ
        └── TicketManagement.Tests.Common/
```

## 移行戦略とリスク管理

### 低リスク移行（即座実行可能）
1. **Application層作成**: 既存コードに影響なし
2. **インターフェース移動**: コンパイル時にエラー検出可能
3. **段階的サービス移動**: 1つずつテスト可能

### リスク軽減策
1. **ブランチでの作業**: `feature/architecture-improvement`
2. **段階的コミット**: 各ステップでコミット
3. **テスト駆動**: 各段階で`dotnet test`実行
4. **ロールバック準備**: 各段階でバックアップ

### 成功指標
1. **ビルド成功**: `dotnet build` エラーなし
2. **テスト通過**: `dotnet test` 全テスト成功  
3. **依存関係正常**: API → Application → Domain の流れ
4. **責任分離**: Infrastructure に業務ロジック無し

## 期待される効果

### 1. 保守性向上
- **変更影響範囲の限定**: ビジネスロジック変更がInfrastructureに波及しない
- **責任の明確化**: 各層の役割が明確

### 2. テスタビリティ向上  
- **Unit test容易**: Application層の独立テスト
- **Mock作成簡単**: インターフェース分離による

### 3. 拡張性向上
- **新機能追加容易**: Application層への追加のみ
- **技術スタック変更容易**: Infrastructure層の交換可能

### 4. チーム開発効率
- **並行開発可能**: 層間の独立性
- **コードレビュー効率**: 責任範囲の明確化