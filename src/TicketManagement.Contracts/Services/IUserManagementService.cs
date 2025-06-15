using TicketManagement.Contracts.DTOs;
using TicketManagement.Core.Enums;

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

    // 新しいユーザー管理機能

    /// <summary>
    /// 新規ユーザーをKeycloakに作成
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(CreateUserDto createUserDto);

    /// <summary>
    /// ユーザーを組織に招待
    /// </summary>
    Task<InviteUserResult> InviteUserToOrganizationAsync(InviteUserDto inviteDto);

    /// <summary>
    /// ユーザーのパスワードをリセット
    /// </summary>
    Task<ResetPasswordResult> ResetUserPasswordAsync(string userId, bool temporary = true);

    /// <summary>
    /// ユーザーの権限を変更
    /// </summary>
    Task<bool> UpdateUserRoleAsync(Guid organizationId, string userId, OrganizationRole newRole);

    /// <summary>
    /// システム管理者権限を付与
    /// </summary>
    Task<bool> GrantSystemAdminAsync(GrantSystemAdminDto grantDto, string grantedBy);

    /// <summary>
    /// システム管理者権限を削除
    /// </summary>
    Task<bool> RevokeSystemAdminAsync(string userId, string revokedBy);

    /// <summary>
    /// システム管理者一覧を取得
    /// </summary>
    Task<IEnumerable<SystemAdminDto>> GetSystemAdminsAsync();

    /// <summary>
    /// ユーザーがシステム管理者かどうかを確認
    /// </summary>
    Task<bool> IsSystemAdminAsync(string userId);

    /// <summary>
    /// ユーザーが組織管理者かどうかを確認
    /// </summary>
    Task<bool> IsOrganizationAdminAsync(string userId, Guid? organizationId = null);
}