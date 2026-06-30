namespace CutLab.Infrastructure.Recognition;

using System.Text;
using System.Text.RegularExpressions;
using CutLab.Domain.ValueObjects;

public static class RecognitionPatternCompiler
{
    private const string VersionSuffix = @"(?:_(?<version>v\d+|draft|s))?$";

    public static Regex? TryCompile(
        string pattern,
        IReadOnlyDictionary<AssetType, string>? typeSuffixes = null) =>
        TryCompile(pattern, typeSuffixes, includeFrameToken: false);

    public static Regex? TryCompileFramePattern(
        string pattern,
        IReadOnlyDictionary<AssetType, string>? typeSuffixes = null) =>
        TryCompile(pattern, typeSuffixes, includeFrameToken: true);

    private static Regex? TryCompile(
        string pattern,
        IReadOnlyDictionary<AssetType, string>? typeSuffixes,
        bool includeFrameToken)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var trimmed = pattern.Trim();
        if (trimmed.StartsWith('^'))
        {
            return TryCreateRegex(trimmed);
        }

        var compiled = CompileTemplate(trimmed, typeSuffixes, includeFrameToken);
        return compiled is null ? null : TryCreateRegex(compiled);
    }

    public static string BuildTypeGroup(IReadOnlyDictionary<AssetType, string> typeSuffixes)
    {
        var merged = new Dictionary<AssetType, string>(DefaultTypeSuffixes());
        foreach (var pair in typeSuffixes)
        {
            merged[pair.Key] = pair.Value;
        }

        var suffixes = merged.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .Select(Regex.Escape)
            .ToList();

        return suffixes.Count == 0
            ? "(?<type>)"
            : $"(?<type>{string.Join("|", suffixes)})";
    }

    private static string? CompileTemplate(
        string template,
        IReadOnlyDictionary<AssetType, string>? typeSuffixes,
        bool includeFrameToken)
    {
        var builder = new StringBuilder("^");
        var typeGroup = BuildTypeGroup(typeSuffixes ?? DefaultTypeSuffixes());

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                builder.Append(Regex.Escape(template[index].ToString()));
                continue;
            }

            var end = template.IndexOf('}', index + 1);
            if (end < 0)
            {
                return null;
            }

            var token = template[(index + 1)..end];
            var colon = token.IndexOf(':');
            if (colon >= 0)
            {
                token = token[..colon];
            }

            var group = token switch
            {
                "N" or "CUT" => @"(?<cut>\d+)",
                "EP" => @"(?<ep>\d+)",
                "SC" => @"(?<sc>\d+)",
                "TYPE" => typeGroup,
                "INSERT" => @"(?<insert>[a-z]?)",
                "FRAME" when includeFrameToken => @"(?<frame>\d+)",
                "SHOT" => @"(?<cut>\d+)",
                _ => null
            };

            if (group is null)
            {
                return null;
            }

            builder.Append(group);
            index = end;
        }

        builder.Append(VersionSuffix);
        return builder.ToString();
    }

    private static Regex? TryCreateRegex(string expression)
    {
        try
        {
            return new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<AssetType, string> DefaultTypeSuffixes() =>
        new Dictionary<AssetType, string>
        {
            [AssetType.Storyboard] = "分镜",
            [AssetType.Keyframe] = "原画",
            [AssetType.Inbetween] = "动画",
            [AssetType.Background] = "背景",
            [AssetType.Render] = "渲染"
        };
}
