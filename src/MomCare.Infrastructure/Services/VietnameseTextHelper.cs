using System.Globalization;
using System.Text;

namespace MomCare.Services;

public static class VietnameseTextHelper
{
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    public static bool ContainsAny(string? text, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalizedText = RemoveDiacritics(text.ToLowerInvariant());
        return keywords.Any(keyword =>
        {
            var normalizedKeyword = RemoveDiacritics(keyword.ToLowerInvariant());
            return text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || normalizedText.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
        });
    }
}
