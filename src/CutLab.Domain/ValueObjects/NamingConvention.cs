namespace CutLab.Domain.ValueObjects;

using CutLab.Domain.Common;

public sealed record NamingConvention(
    string Template,
    string Separator,
    IReadOnlyDictionary<AssetType, string> TypeSuffixes)
{
    public static Result<NamingConvention> Create(
        string template,
        string separator,
        IReadOnlyDictionary<AssetType, string> typeSuffixes)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return Result.Failure<NamingConvention>("命名模板不能为空。");
        }

        if (!template.Contains("{CUT", StringComparison.Ordinal))
        {
            return Result.Failure<NamingConvention>("命名模板必须包含 {CUT} 占位符。");
        }

        return Result.Success(new NamingConvention(template, separator, typeSuffixes));
    }
}
