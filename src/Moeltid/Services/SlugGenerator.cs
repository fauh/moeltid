using System.Text.RegularExpressions;

namespace Moeltid.Services;

public partial class SlugGenerator(TokenGenerator tokens)
{
    private const int MaxTitleLength = 40;
    private const int RandomSuffixLength = 6;

    public string Generate(string? title)
    {
        var suffix = tokens.RandomUrlSafeString(RandomSuffixLength);
        var kebab = ToKebab(title);
        return string.IsNullOrEmpty(kebab) ? $"event-{suffix}" : $"{kebab}-{suffix}";
    }

    private static string ToKebab(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var lower = title.ToLowerInvariant();
        var ascii = NonAsciiAlnum().Replace(lower, " ");
        var words = ascii.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kebab = string.Join("-", words);

        return kebab.Length > MaxTitleLength
            ? kebab[..MaxTitleLength].TrimEnd('-')
            : kebab;
    }

    [GeneratedRegex(@"[^a-z0-9\s]")]
    private static partial Regex NonAsciiAlnum();
}
