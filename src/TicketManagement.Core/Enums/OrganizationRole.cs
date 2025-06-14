namespace TicketManagement.Core.Enums;

/// <summary>
/// Organization内でのユーザーの役割
/// </summary>
public enum OrganizationRole
{
    /// <summary>
    /// 閲覧のみ可能なメンバー
    /// </summary>
    Viewer = 0,
    
    /// <summary>
    /// プロジェクトに参加できる一般メンバー
    /// </summary>
    Member = 1,
    
    /// <summary>
    /// プロジェクトを作成・管理できるマネージャー
    /// </summary>
    Manager = 2,
    
    /// <summary>
    /// Organization全体を管理できる管理者
    /// </summary>
    Admin = 3
}