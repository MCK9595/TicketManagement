using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TicketManagement.Contracts.Validation;

/// <summary>
/// プロジェクト名の形式を検証する属性
/// </summary>
public class ProjectNameAttribute : ValidationAttribute
{
    private static readonly Regex ProjectNameRegex = new(@"^[a-zA-Z0-9\s\-_\.]+$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is string projectName)
        {
            return !string.IsNullOrWhiteSpace(projectName) && 
                   projectName.Length <= 100 && 
                   ProjectNameRegex.IsMatch(projectName);
        }
        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must contain only alphanumeric characters, spaces, hyphens, underscores, and dots, and be 1-100 characters long.";
    }
}

/// <summary>
/// チケットタイトルの形式を検証する属性
/// </summary>
public class TicketTitleAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is string title)
        {
            return !string.IsNullOrWhiteSpace(title) && 
                   title.Length <= 200 && 
                   title.Trim().Length >= 3;
        }
        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be 3-200 characters long and not consist only of whitespace.";
    }
}

/// <summary>
/// タグの形式を検証する属性
/// </summary>
public class TagsAttribute : ValidationAttribute
{
    private static readonly Regex TagRegex = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is string[] tags)
        {
            if (tags.Length > 10) return false; // 最大10個のタグ

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || 
                    tag.Length > 50 || 
                    !TagRegex.IsMatch(tag))
                {
                    return false;
                }
            }
            return true;
        }
        return true; // nullまたは空配列は有効
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must contain at most 10 tags, each 1-50 characters long with only alphanumeric characters, hyphens, and underscores.";
    }
}

/// <summary>
/// 未来の日付のみを許可する属性
/// </summary>
public class FutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime > DateTime.UtcNow;
        }
        return true; // nullは有効（任意フィールド）
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a future date.";
    }
}

/// <summary>
/// HTMLコンテンツをサニタイズする属性
/// </summary>
public class SafeHtmlAttribute : ValidationAttribute
{
    private static readonly string[] DangerousTags = { "<script", "<iframe", "<object", "<embed", "<form" };

    public override bool IsValid(object? value)
    {
        if (value is string content)
        {
            var lowerContent = content.ToLowerInvariant();
            return !DangerousTags.Any(tag => lowerContent.Contains(tag));
        }
        return true;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} contains potentially dangerous HTML content.";
    }
}