using System.Security.Cryptography;

namespace TicketManagement.Infrastructure.Utilities;

public static class PasswordManager
{
    /// <summary>
    /// 安全な一時パスワードを生成
    /// </summary>
    public static string GenerateTemporaryPassword(int length = 12)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
    
    /// <summary>
    /// パスワード強度をチェック
    /// </summary>
    public static bool ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) 
            return false;
        
        if (!password.Any(char.IsUpper)) 
            return false;
        
        if (!password.Any(char.IsLower)) 
            return false;
        
        if (!password.Any(char.IsDigit)) 
            return false;
        
        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c))) 
            return false;
        
        return true;
    }
}