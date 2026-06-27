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
        string extension,
        VersionTag? versionTag = null)
    {
        if (!convention.TypeSuffixes.TryGetValue(type, out var typeSuffix))
        {
            return Result.Failure<FileName>($"未配置资产类型 {type} 的后缀。");
        }

        var cutToken = cut.Cut.ToString("D3");
        if (!string.IsNullOrEmpty(cut.InsertSuffix))
        {
            cutToken += cut.InsertSuffix;
        }

        var body = convention.Template
            .Replace("{EP:02}", cut.Episode.ToString("D2"), StringComparison.Ordinal)
            .Replace("{SC:02}", cut.Scene.ToString("D2"), StringComparison.Ordinal)
            .Replace("{CUT:03}", cutToken, StringComparison.Ordinal)
            .Replace("{TYPE}", typeSuffix, StringComparison.Ordinal);

        if (versionTag is not null)
        {
            body += $"_{versionTag.Value.Value}";
        }

        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return Result.Success(new FileName($"{body}{normalizedExtension}"));
    }
}
