# TicketManagement プロジェクト構造改善計画

## 現在の問題分析

### 1. **Clean Architecture違反**
- ApiServiceがInfrastructureを直接参照している
- 業務ロジック（Services）がInfrastructure層に配置されている
- Interfaceの配置が不適切

### 2. **プロジェクト配置の不統一**
- プレゼンテーション層（API、Web）がルートレベル
- Core業務ロジックがsrc/フォルダ内
- テストプロジェクトが別の場所

### 3. **責任の分離不足**
- Infrastructure層に業務ロジックが混在
- キャッシュ、データアクセス、業務サービスが同一レイヤー

## 推奨改善プラン

### **フェーズ1: 最小限の構造改善（即座実行可能）**

#### 1.1 Application層の作成
```
src/TicketManagement.Application/
├── Services/           # 業務ロジックサービス
├── Interfaces/         # アプリケーションインターface
├── DTOs/              # 既存ContractsのDTOsを移動
├── Validators/        # 入力検証ロジック
└── Mappings/          # エンティティ-DTO変換
```

#### 1.2 Infrastructure層の分離
```
src/TicketManagement.Infrastructure/
├── Data/              # データアクセスのみ
│   ├── Contexts/
│   ├── Configurations/
│   ├── Repositories/
│   └── Migrations/
├── Caching/           # キャッシュ関連
├── External/          # 外部サービス連携
└── Common/            # 共通インフラ機能
```

#### 1.3 依存関係修正
```
Api → Application → Domain
Infrastructure → Application & Domain（実装）
Web → Application → Domain
```

### **フェーズ2: 完全な再構成（長期計画）**

#### 2.1 理想的なフォルダ構造
```
TicketManagement/
├── src/
│   ├── Core/
│   │   ├── TicketManagement.Domain/         # エンティティ、enums
│   │   └── TicketManagement.Application/    # 業務ロジック
│   ├── Infrastructure/
│   │   ├── TicketManagement.Infrastructure.Data/
│   │   ├── TicketManagement.Infrastructure.Caching/
│   │   └── TicketManagement.Infrastructure.Messaging/
│   └── Presentation/
│       ├── TicketManagement.Api/
│       └── TicketManagement.Web/
├── hosting/
│   ├── TicketManagement.AppHost/
│   └── TicketManagement.ServiceDefaults/
└── tests/
    ├── Unit/
    ├── Integration/
    └── Common/
```

## 実装手順

### ステップ1: Application層作成
1. TicketManagement.Application プロジェクト作成
2. 業務サービスの移動（ProjectService, TicketService等）
3. インターフェースの再配置
4. 依存関係の修正

### ステップ2: Infrastructure整理
1. Infrastructure層から業務ロジック削除
2. データアクセス関心事のみ残す
3. キャッシュサービスの分離

### ステップ3: プレゼンテーション層修正
1. API依存関係をApplicationのみに変更
2. 直接Infrastructure参照の削除

### ステップ4: テスト構造改善
1. レイヤー別テスト分離
2. 統合テストとユニットテストの明確な分離

## 期待される効果

### 1. **保守性向上**
- 責任の明確な分離
- 変更影響範囲の限定

### 2. **テストability向上**
- レイヤー独立したテスト
- Mock作成の簡素化

### 3. **拡張性向上**
- 新機能追加時の影響最小化
- 外部依存の交換容易性

### 4. **チーム開発効率**
- 並行開発の干渉最小化
- コードレビューの効率化

## 移行戦略

### 段階的移行アプローチ
1. **週1**: Application層作成、インターフェース移動
2. **週2**: 業務サービス移動、依存関係修正
3. **週3**: Infrastructure層クリーンアップ
4. **週4**: テスト構造改善、統合テスト実行

### リスク軽減
- ブランチベースでの段階的実装
- 各段階でのテスト実行
- ロールバック計画の準備