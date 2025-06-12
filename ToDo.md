# TicketManagement System - Development ToDo List

## 現在の進捗状況
✅ **完了済み**
- [x] Aspireスターターテンプレートでプロジェクト作成
- [x] 追加プロジェクト作成（Core, Infrastructure, Contracts, Tests）
- [x] ドメインモデル実装（エンティティ、Enum）
- [x] Entity Framework Core設定とDbContext実装
- [x] AppHost設定（SQL Server, Redis, Keycloak）

## Phase 1: Core システム開発

### 1. Infrastructure層の実装
- [x] リポジトリインターフェース定義 (`Contracts`プロジェクト)
- [x] リポジトリ実装 (`Infrastructure`プロジェクト)
  - [x] ProjectRepository
  - [x] TicketRepository
  - [x] CommentRepository
  - [x] NotificationRepository
- [x] サービス層実装
  - [x] TicketService
  - [x] ProjectService
  - [x] NotificationService
  - [x] TicketHistoryService
  - [x] TicketSearchService
- [x] データベースマイグレーション作成と適用

### 2. API Service実装
- [x] 認証・認可設定
  - [x] JWT Bearer認証設定
  - [x] Keycloak統合設定
  - [x] 認可ポリシー定義
- [x] DTOs実装 (`Contracts`プロジェクト)
  - [x] Project関連DTO
  - [x] Ticket関連DTO
  - [x] Comment関連DTO
  - [x] Notification関連DTO
  - [x] Search/Filter関連DTO
- [x] コントローラー実装
  - [x] ProjectsController
  - [x] TicketsController
  - [x] CommentsController
  - [x] NotificationsController
  - [x] ReportsController
- [x] SignalR Hub実装
  - [x] NotificationHub
  - [x] リアルタイム通知機能

### 3. Web Frontend実装 (Blazor Server)
- [x] 認証設定
  - [x] Keycloak認証連携
  - [x] 認証状態管理
- [x] 共通コンポーネント
  - [x] Layout設定
  - [x] ナビゲーション
  - [x] ページング
  - [x] フィルターコンポーネント
- [x] 画面実装
  - [x] ダッシュボード
  - [x] プロジェクト管理画面
    - [x] プロジェクト一覧
    - [x] プロジェクト作成/編集
    - [x] メンバー管理
  - [x] チケット管理画面
    - [x] チケット一覧（フィルタ・検索機能付き）
    - [x] チケット詳細・編集
    - [x] コメントスレッド表示
  - [x] 通知センター
  - [x] レポート画面

### 4. テスト実装
- [x] 単体テスト
  - [x] Core層テスト
    - [x] TicketTests
    - [x] ProjectTests
    - [x] CommentTests
    - [x] ProjectMemberTests
    - [x] TicketHistoryTests
    - [x] TicketAssignmentTests
    - [x] NotificationTests
    - [x] エンティティ関係テスト
  - [x] Infrastructure層テスト
    - [x] RepositoryTests（InMemory DB使用）
    - [x] ServiceTests（Mockを使用）
  - [x] API層テスト
    - [x] ControllerTests（Mockを使用）
    - [x] 認証・認可テスト
- [ ] 統合テスト（Aspire DI複雑性のため一時保留）
  - [ ] APIエンドポイント統合テスト
  - [ ] SignalR通信テスト
  - [ ] データベース操作テスト
  - [ ] パフォーマンステスト

**テスト実装結果**: 185テスト合格、18テストスキップ（統合テスト17 + リポジトリテスト1）

### 5. セキュリティ・パフォーマンス対策
- [x] セキュリティ実装
  - [x] 入力値検証（カスタム検証属性実装）
  - [x] CSRF対策（セキュリティヘッダー、トークン検証）
  - [x] 権限チェック強化（ロールベース認可ポリシー）
  - [x] レート制限実装
  - [x] セキュリティヘッダー設定（XSS、CSP、HSTS等）
- [x] パフォーマンス最適化
  - [x] データベースインデックス最適化（全エンティティ）
  - [x] キャッシュ戦略実装（Redis統合）
  - [x] 遅延読み込み設定
  - [x] クエリ最適化（複合インデックス）
  - [x] パフォーマンス向上マイグレーション作成

### 5.5 アーキテクチャ改善（推奨）
- [x] プロジェクト構造分析完了
- [x] Clean Architecture違反の特定
- [x] 改善計画書作成（ARCHITECTURE_IMPROVEMENT_PLAN.md）
- [x] 具体的実装ガイド作成（CONCRETE_STRUCTURE_IMPROVEMENTS.md）
- [ ] Application層作成と業務ロジック分離
- [ ] Infrastructure層のクリーンアップ
- [ ] 依存関係の修正（API → Application → Domain）

### 5.6 包括的ログシステム（完了）
- [x] ログシステム設計・分析完了
- [x] 構造化ログ基盤実装
  - [x] LogEvent モデル（セキュリティ、監査、パフォーマンス、ビジネス）
  - [x] StructuredLogger サービス（PII保護、機密情報フィルタリング）
  - [x] LogEnrichmentService（コンテキスト自動追加）
- [x] 専門ログサービス実装
  - [x] AuditLogService（プロジェクト・チケット・コメント操作の監査）
  - [x] SecurityLogService（認証・認可・不正アクセス検知）
  - [x] PerformanceLogService（API・DB・キャッシュ・SignalRパフォーマンス）
- [x] ログミドルウェア実装
  - [x] EnhancedLoggingMiddleware（リクエスト・レスポンス・セキュリティチェック）
  - [x] AuthenticationLoggingMiddleware（認証状態変化の追跡）
  - [x] PerformanceMonitoringMiddleware（システムリソース監視）
- [x] ログ設定・統合
  - [x] 包括的ログ設定（本番・開発環境別）
  - [x] DIコンテナ統合
  - [x] ミドルウェアパイプライン統合
  - [x] appsettings.json設定完了
- [x] ログ統合実装例・ドキュメント作成
- [x] セッション設定エラー修正（安全なセッションアクセス実装）

### 6. デプロイメント準備
- [ ] 環境設定
  - [ ] 本番環境用AppHost設定
  - [ ] 環境変数設定
  - [ ] 接続文字列設定
- [ ] Keycloak設定
  - [ ] Realm設定ファイル作成
  - [ ] Client設定
  - [ ] ユーザー・ロール設定
- [ ] CI/CDパイプライン
  - [ ] GitHub Actions設定
  - [ ] テスト自動実行
  - [ ] カバレッジレポート生成

## Phase 2: 拡張機能

### 7. 高度な機能実装
- [ ] ファイル添付機能
  - [ ] ファイルアップロード機能
  - [ ] ファイルストレージ設定
  - [ ] セキュリティ設定
- [ ] メール通知機能
  - [ ] SMTP設定
  - [ ] 通知テンプレート
  - [ ] 非同期メール送信
- [ ] レポート機能強化
  - [ ] バーンダウンチャート
  - [ ] 統計ダッシュボード
  - [ ] データエクスポート機能

### 8. UI/UX改善
- [ ] レスポンシブデザイン
- [ ] モバイル対応
- [ ] アクセシビリティ対応
- [ ] パフォーマンス最適化（フロントエンド）

## Phase 3: 高度な統合機能

### 9. AI・自動化機能
- [ ] AI による自動分類・提案
- [ ] チケット自動割り当て
- [ ] 優先度自動設定

### 10. 外部連携
- [ ] GitHub連携
- [ ] Jira連携
- [ ] Slack連携

### 11. ワークフロー自動化
- [ ] カスタムワークフロー
- [ ] 自動化ルール設定
- [ ] カスタムフィールド対応

## 開発上の注意事項

### 必須事項
1. **テスト実行**: すべてのソースコード修正後は必ず`dotnet test`を実行
2. **テストコード追加**: 新機能追加時は対応するテストコードを必ず追加
3. **コードカバレッジ**: 80%以上の目標を維持
4. **認証・認可**: すべてのAPIエンドポイントに適切な権限チェックを実装

### 開発フロー
1. Core層でドメインロジック実装
2. Infrastructure層でデータアクセス実装
3. API層でエンドポイント実装
4. Web層でUI実装
5. 各層でテスト実装
6. 統合テスト実行

### 品質管理
- Clean Architectureパターンの維持
- DRY原則の遵守
- SOLID原則の遵守
- 適切なエラーハンドリング実装