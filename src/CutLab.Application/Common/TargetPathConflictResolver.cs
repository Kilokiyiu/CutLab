namespace CutLab.Application.Common;

using CutLab.Domain.ValueObjects;

public sealed record TargetPathResolution(
    FilePath TargetPath,
    FileName ProposedFileName,
    RenamePlanStatus Status,
    string? Message);

public sealed record ArchiveTargetPathResolution(
    FilePath TargetPath,
    FileName FileName,
    ArchivePlanStatus Status,
    string? Message);

public static class TargetPathConflictResolver
{
    public static TargetPathResolution ResolveRename(
        string proposedTargetPath,
        FileName proposedFileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        ConflictResolutionStrategy strategy,
        ISet<string>? pathsBeingMoved = null)
    {
        if (reservedTargets.TryGetValue(proposedTargetPath, out var existingAssetId)
            && existingAssetId != assetId)
        {
            return ResolveExternalConflict(
                proposedTargetPath,
                proposedFileName,
                assetId,
                reservedTargets,
                strategy,
                "与另一文件目标名冲突。");
        }

        if (File.Exists(proposedTargetPath) && !IsPathBeingMoved(proposedTargetPath, pathsBeingMoved))
        {
            return ResolveExternalConflict(
                proposedTargetPath,
                proposedFileName,
                assetId,
                reservedTargets,
                strategy,
                "目标文件已存在。");
        }

        reservedTargets[proposedTargetPath] = assetId;
        return new TargetPathResolution(
            new FilePath(proposedTargetPath),
            proposedFileName,
            RenamePlanStatus.Ready,
            null);
    }

    public static ArchiveTargetPathResolution ResolveArchiveMove(
        string proposedTargetPath,
        FileName fileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        ConflictResolutionStrategy strategy,
        ISet<string>? pathsBeingMoved = null)
    {
        if (reservedTargets.TryGetValue(proposedTargetPath, out var existingAssetId)
            && existingAssetId != assetId)
        {
            return ResolveArchiveExternalConflict(
                proposedTargetPath,
                fileName,
                assetId,
                reservedTargets,
                strategy,
                "与另一文件目标路径冲突。");
        }

        if (File.Exists(proposedTargetPath) && !IsPathBeingMoved(proposedTargetPath, pathsBeingMoved))
        {
            return ResolveArchiveExternalConflict(
                proposedTargetPath,
                fileName,
                assetId,
                reservedTargets,
                strategy,
                "目标文件已存在。");
        }

        reservedTargets[proposedTargetPath] = assetId;
        return new ArchiveTargetPathResolution(
            new FilePath(proposedTargetPath),
            fileName,
            ArchivePlanStatus.Ready,
            null);
    }

    private static TargetPathResolution ResolveExternalConflict(
        string proposedTargetPath,
        FileName proposedFileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        ConflictResolutionStrategy strategy,
        string reason)
    {
        return strategy switch
        {
            ConflictResolutionStrategy.Skip => new TargetPathResolution(
                new FilePath(proposedTargetPath),
                proposedFileName,
                RenamePlanStatus.Skipped,
                $"{reason}已跳过。"),
            ConflictResolutionStrategy.AutoSuffix => ResolveWithSuffix(
                proposedTargetPath,
                proposedFileName,
                assetId,
                reservedTargets,
                reason),
            _ => new TargetPathResolution(
                new FilePath(proposedTargetPath),
                proposedFileName,
                RenamePlanStatus.Conflict,
                reason)
        };
    }

    private static ArchiveTargetPathResolution ResolveArchiveExternalConflict(
        string proposedTargetPath,
        FileName fileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        ConflictResolutionStrategy strategy,
        string reason)
    {
        return strategy switch
        {
            ConflictResolutionStrategy.Skip => new ArchiveTargetPathResolution(
                new FilePath(proposedTargetPath),
                fileName,
                ArchivePlanStatus.Skipped,
                $"{reason}已跳过。"),
            ConflictResolutionStrategy.AutoSuffix => ResolveArchiveWithSuffix(
                proposedTargetPath,
                fileName,
                assetId,
                reservedTargets,
                reason),
            _ => new ArchiveTargetPathResolution(
                new FilePath(proposedTargetPath),
                fileName,
                ArchivePlanStatus.Conflict,
                reason)
        };
    }

    private static TargetPathResolution ResolveWithSuffix(
        string proposedTargetPath,
        FileName proposedFileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        string reason)
    {
        var reservedPaths = new HashSet<string>(reservedTargets.Keys, StringComparer.OrdinalIgnoreCase);
        var resolvedPath = FindAvailablePath(proposedTargetPath, reservedPaths);
        if (resolvedPath is null)
        {
            return new TargetPathResolution(
                new FilePath(proposedTargetPath),
                proposedFileName,
                RenamePlanStatus.Conflict,
                $"{reason}无法生成可用后缀名。");
        }

        reservedTargets[resolvedPath] = assetId;
        var resolvedFileName = new FileName(Path.GetFileName(resolvedPath));
        return new TargetPathResolution(
            new FilePath(resolvedPath),
            resolvedFileName,
            RenamePlanStatus.Ready,
            string.Equals(resolvedPath, proposedTargetPath, StringComparison.OrdinalIgnoreCase)
                ? null
                : $"{reason}已自动改为 {resolvedFileName.Value}。");
    }

    private static ArchiveTargetPathResolution ResolveArchiveWithSuffix(
        string proposedTargetPath,
        FileName fileName,
        Guid assetId,
        IDictionary<string, Guid> reservedTargets,
        string reason)
    {
        var reservedPaths = new HashSet<string>(reservedTargets.Keys, StringComparer.OrdinalIgnoreCase);
        var resolvedPath = FindAvailablePath(proposedTargetPath, reservedPaths);
        if (resolvedPath is null)
        {
            return new ArchiveTargetPathResolution(
                new FilePath(proposedTargetPath),
                fileName,
                ArchivePlanStatus.Conflict,
                $"{reason}无法生成可用后缀名。");
        }

        reservedTargets[resolvedPath] = assetId;
        var resolvedFileName = new FileName(Path.GetFileName(resolvedPath));
        return new ArchiveTargetPathResolution(
            new FilePath(resolvedPath),
            resolvedFileName,
            ArchivePlanStatus.Ready,
            string.Equals(resolvedPath, proposedTargetPath, StringComparison.OrdinalIgnoreCase)
                ? null
                : $"{reason}已自动改为 {resolvedFileName.Value}。");
    }

    public static string? FindAvailablePath(string proposedTargetPath, ISet<string> reservedPaths)
    {
        if (!reservedPaths.Contains(proposedTargetPath) && !File.Exists(proposedTargetPath))
        {
            return proposedTargetPath;
        }

        var directory = Path.GetDirectoryName(proposedTargetPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(proposedTargetPath);
        var extension = Path.GetExtension(proposedTargetPath);

        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            if (!reservedPaths.Contains(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsPathBeingMoved(string path, ISet<string>? pathsBeingMoved) =>
        pathsBeingMoved is not null && pathsBeingMoved.Contains(path);
}
