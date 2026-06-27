namespace CutLab.Domain.ValueObjects;

public static partial class VersionTagParser
{
    private static readonly HashSet<string> KnownTags =
        new(StringComparer.OrdinalIgnoreCase) { "s", "draft" };

    public static VersionTag? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().TrimStart('_', '-');
        if (normalized.Length == 0)
        {
            return null;
        }

        if (KnownTags.Contains(normalized) || VersionTagPattern().IsMatch(normalized))
        {
            return new VersionTag(normalized.ToLowerInvariant());
        }

        return null;
    }

    public static bool MatchesFilter(VersionTag? tag, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (tag is null)
        {
            return false;
        }

        return string.Equals(tag.Value.Value, filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^v\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex VersionTagPattern();
}
