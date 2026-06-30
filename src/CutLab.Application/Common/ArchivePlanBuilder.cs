namespace CutLab.Application.Common;

using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record ArchivePlanItem(
    FilePath? SourcePath,
    FilePath TargetPath,
    FileName? FileName,
    ArchivePlanStatus Status,
    ArchiveOperationKind OperationKind,
    string? Message);

public enum ArchivePlanStatus
{
    Ready,
    AlreadyInPlace,
    Conflict,
    Skipped
}

public enum ArchiveOperationKind
{
    MoveFile,
    CreateDirectory
}

public enum ArchiveExecutionMode
{
    CreateDirectoriesOnly,
    MoveFiles
}

public static class ArchivePlanBuilder
{
    public static IReadOnlyList<ArchivePlanItem> Build(
        AnimationProject project,
        ScanSession session,
        IArchivePathResolver pathResolver,
        ArchiveExecutionMode mode,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Fail)
    {
        return mode switch
        {
            ArchiveExecutionMode.CreateDirectoriesOnly => BuildDirectoryPlan(project, session, pathResolver),
            ArchiveExecutionMode.MoveFiles => BuildMovePlan(project, session, pathResolver, conflictStrategy),
            _ => []
        };
    }

    private static IReadOnlyList<ArchivePlanItem> BuildDirectoryPlan(
        AnimationProject project,
        ScanSession session,
        IArchivePathResolver pathResolver)
    {
        var items = new List<ArchivePlanItem>();
        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in session.GetRecognized())
        {
            if (asset.ParsedCut is null || asset.AssetType is null)
            {
                continue;
            }

            foreach (var folder in project.ArchiveTemplate.FolderNames)
            {
                if (!TryMapFolderToAssetType(folder, out var assetType))
                {
                    continue;
                }

                var resolved = pathResolver.ResolveDirectory(
                    project.ArchiveTemplate,
                    project.NamingConvention,
                    project.RootPath,
                    asset.ParsedCut.Value,
                    assetType);

                if (resolved.IsFailure)
                {
                    continue;
                }

                var directoryPath = resolved.Value!.Value;
                if (!created.Add(directoryPath))
                {
                    continue;
                }

                FilePath targetDirectory = resolved.Value!;
                items.Add(new ArchivePlanItem(
                    default,
                    targetDirectory,
                    default,
                    Directory.Exists(directoryPath) ? ArchivePlanStatus.AlreadyInPlace : ArchivePlanStatus.Ready,
                    ArchiveOperationKind.CreateDirectory,
                    Directory.Exists(directoryPath) ? "目录已存在。" : null));
            }
        }

        return items;
    }

    private static IReadOnlyList<ArchivePlanItem> BuildMovePlan(
        AnimationProject project,
        ScanSession session,
        IArchivePathResolver pathResolver,
        ConflictResolutionStrategy conflictStrategy)
    {
        var items = new List<ArchivePlanItem>();
        var targetPaths = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in session.GetRecognized())
        {
            if (asset.ParsedCut is null || asset.AssetType is null)
            {
                continue;
            }

            var fileName = asset.ProposedFileName?.Value
                ?? Path.GetFileName(asset.OriginalPath.Value);

            var resolved = pathResolver.ResolveDirectory(
                project.ArchiveTemplate,
                project.NamingConvention,
                project.RootPath,
                asset.ParsedCut.Value,
                asset.AssetType.Value);

            if (resolved.IsFailure)
            {
                items.Add(new ArchivePlanItem(
                    asset.OriginalPath,
                    asset.OriginalPath,
                    new FileName(fileName),
                    ArchivePlanStatus.Skipped,
                    ArchiveOperationKind.MoveFile,
                    resolved.Error ?? "无法解析归档路径。"));
                continue;
            }

            var resolvedPath = resolved.Value!;
            var targetFilePath = Path.Combine(resolvedPath.Value, fileName);
            var proposedFileName = new FileName(fileName);

            if (string.Equals(asset.OriginalPath.Value, targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ArchivePlanItem(
                    asset.OriginalPath,
                    new FilePath(targetFilePath),
                    proposedFileName,
                    ArchivePlanStatus.AlreadyInPlace,
                    ArchiveOperationKind.MoveFile,
                    "文件已在目标位置。"));
                continue;
            }

            var resolution = TargetPathConflictResolver.ResolveArchiveMove(
                targetFilePath,
                proposedFileName,
                asset.Id,
                targetPaths,
                conflictStrategy);

            items.Add(new ArchivePlanItem(
                asset.OriginalPath,
                resolution.TargetPath,
                resolution.FileName,
                resolution.Status,
                ArchiveOperationKind.MoveFile,
                resolution.Message));
        }

        return items;
    }

    private static bool TryMapFolderToAssetType(string folder, out AssetType assetType)
    {
        assetType = folder switch
        {
            "分镜" => AssetType.Storyboard,
            "原画" => AssetType.Keyframe,
            "动画" => AssetType.Inbetween,
            "背景" => AssetType.Background,
            "渲染" => AssetType.Render,
            _ => default
        };

        return folder is "分镜" or "原画" or "动画" or "背景" or "渲染";
    }
}
