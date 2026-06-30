namespace CutLab.Application.Common;

using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record RenamePlanItem(
    Guid AssetId,
    FilePath SourcePath,
    FilePath TargetPath,
    FileName ProposedFileName,
    RenamePlanStatus Status,
    string? Message);

public enum RenamePlanStatus
{
    Ready,
    AlreadyNamed,
    Conflict,
    Skipped
}

public static class RenamePlanBuilder
{
    public static IReadOnlyList<RenamePlanItem> Build(
        ScanSession session,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Fail)
    {
        var items = new List<RenamePlanItem>();
        var targetPaths = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in session.Assets)
        {
            if (asset.RecognitionStatus != RecognitionStatus.Recognized
                || asset.ProposedFileName is null)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(asset.OriginalPath.Value) ?? string.Empty;
            var targetFullPath = Path.Combine(directory, asset.ProposedFileName.Value.Value);
            var targetPath = new FilePath(targetFullPath);

            if (string.Equals(
                    asset.OriginalPath.Value,
                    targetFullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new RenamePlanItem(
                    asset.Id,
                    asset.OriginalPath,
                    targetPath,
                    asset.ProposedFileName.Value,
                    RenamePlanStatus.AlreadyNamed,
                    "已是规范命名。"));
                continue;
            }

            var resolution = TargetPathConflictResolver.ResolveRename(
                targetFullPath,
                asset.ProposedFileName.Value,
                asset.Id,
                targetPaths,
                conflictStrategy);

            items.Add(new RenamePlanItem(
                asset.Id,
                asset.OriginalPath,
                resolution.TargetPath,
                resolution.ProposedFileName,
                resolution.Status,
                resolution.Message));
        }

        return items;
    }
}
