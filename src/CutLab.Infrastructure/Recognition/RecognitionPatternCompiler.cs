namespace CutLab.Infrastructure.Recognition;

using System.Text;
using System.Text.RegularExpressions;

public static class RecognitionPatternCompiler
{
    private const string TypeGroup = @"(?<type>分镜|原画|动画|背景|渲染)";
    private const string VersionSuffix = @"(?:_(?<version>v\d+|draft|s))?$";

    public static Regex? TryCompile(string pattern)
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

        var compiled = CompileTemplate(trimmed);
        return compiled is null ? null : TryCreateRegex(compiled);
    }

    private static string? CompileTemplate(string template)
    {
        var builder = new StringBuilder("^");

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
                "TYPE" => TypeGroup,
                "INSERT" => @"(?<insert>[a-z]?)",
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
}
