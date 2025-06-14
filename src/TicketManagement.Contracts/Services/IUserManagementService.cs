using TicketManagement.Contracts.DTOs;

namespace TicketManagement.Contracts.Services;

public interface IUserManagementService
{
    /// <summary>
    /// Keycloakからユーザー情報を取得
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(string userId);

    /// <summary>
    /// ユーザー名またはメールでユーザーを検索
    /// </summary>
    Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm, int maxResults = 20);

    /// <summary>
    /// ユーザー情報の一括取得
    /// </summary>
    Task<Dictionary<string, UserDto>> GetUsersByIdsAsync(IEnumerable<string> userIds);

    /// <summary>
    /// 現在のユーザー情報を取得
    /// </summary>
    Task<UserDto?> GetCurrentUserAsync();

    /// <summary>
    /// ユーザーの詳細情報を取得（組織・プロジェクト情報含む）
    /// </summary>
    Task<UserDetailDto?> GetUserDetailAsync(string userId);

    /// <summary>
    /// ユーザーの組織一覧を取得
    /// </summary>
    Task<IEnumerable<UserOrganizationDto>> GetUserOrganizationsAsync(string userId);

    /// <summary>
    /// ユーザーの有効性を確認
    /// </summary>
    Task<bool> IsUserActiveAsync(string userId);
}