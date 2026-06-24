namespace CutLab.Infrastructure.Naming;

using CutLab.Domain.Common;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed class TemplateNamingService : INamingService
{
    public Result<FileName> GenerateFileName(
        NamingConvention convention,
        CutNumber cut,
        AssetType type,
        string extension)
    {
        if (!convention.TypeSuffixes.TryGetValue(type, out var typeSuffix))
        {
            return Result.Failure<FileName>($"未配置资产类型 {type} 的后缀。");
        }

        var body = convention.Template
            .Replace("{EP:02}", cut.Episode.ToString("D2"), StringComparison.Ordinal)
            .Replace("{SC:02}", cut.Scene.ToString("D2"), StringComparison.Ordinal)
            .Replace("{CUT:03}", cut.Cut.ToString("D3"), StringComparison.Ordinal)
            .Replace("{TYPE}", typeSuffix, StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(cut.InsertSuffix))
        {
            body += cut.InsertSuffix;
        }

        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return Result.Success(new FileName($"{body}{normalizedExtension}"));
    }
}
