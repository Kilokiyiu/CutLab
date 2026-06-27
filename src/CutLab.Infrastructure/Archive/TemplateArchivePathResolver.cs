namespace CutLab.Infrastructure.Archive;

using CutLab.Domain.Common;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed class TemplateArchivePathResolver : IArchivePathResolver
{
    public Result<FilePath> ResolveDirectory(
        ArchiveTemplate template,
        NamingConvention namingConvention,
        WorkspacePath projectRoot,
        CutNumber cut,
        AssetType assetType)
    {
        if (!namingConvention.TypeSuffixes.TryGetValue(assetType, out var typeSuffix))
        {
            return Result.Failure<FilePath>($"未配置资产类型 {assetType} 的归档后缀。");
        }

        var relativePath = template.PathPattern
            .Replace("{EP:02}", cut.Episode.ToString("D2"), StringComparison.Ordinal)
            .Replace("{SC:02}", cut.Scene.ToString("D2"), StringComparison.Ordinal)
            .Replace("{CUT:03}", cut.Cut.ToString("D3"), StringComparison.Ordinal)
            .Replace("{TYPE}", typeSuffix, StringComparison.Ordinal)
            .TrimEnd('/', '\\');

        if (!string.IsNullOrEmpty(cut.InsertSuffix))
        {
            relativePath += cut.InsertSuffix;
        }

        var fullPath = Path.Combine(projectRoot.Value, relativePath);
        return Result.Success(new FilePath(fullPath));
    }
}
