namespace CutLab.Application.Common;

using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record ScanInventoryItem(
    Guid AssetId,
    string CutId,
    FilePath SourcePath,
    string TargetDisplay,
    string Status,
    string Message);

public static class ScanInventoryBuilder
{
    public static IReadOnlyList<ScanInventoryItem> Build(ScanSession session)
    {
        var renameItems = RenamePlanBuilder.Build(session).ToDictionary(item => item.AssetId);
        var inventory = new List<ScanInventoryItem>();

        foreach (var asset in session.Assets
                     .OrderBy(a => a.ParsedCut?.Cut ?? int.MaxValue)
                     .ThenBy(a => a.ParsedCut?.InsertSuffix ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(a => a.OriginalPath.Value, StringComparer.OrdinalIgnoreCase))
        {
            var cutId = asset.ParsedCut?.ToString() ?? string.Empty;
            var fileName = Path.GetFileName(asset.OriginalPath.Value);

            if (renameItems.TryGetValue(asset.Id, out var renameItem))
            {
                inventory.Add(new ScanInventoryItem(
                    asset.Id,
                    cutId,
                    asset.OriginalPath,
                    renameItem.ProposedFileName.Value,
                    renameItem.Status.ToString(),
                    renameItem.Message ?? string.Empty));
                continue;
            }

            if (asset.RecognitionStatus == RecognitionStatus.Unrecognized)
            {
                inventory.Add(new ScanInventoryItem(
                    asset.Id,
                    cutId,
                    asset.OriginalPath,
                    "—",
                    "Unrecognized",
                    "未匹配命名规则。"));
                continue;
            }

            inventory.Add(new ScanInventoryItem(
                asset.Id,
                cutId,
                asset.OriginalPath,
                asset.ProposedFileName?.Value ?? "—",
                "Skipped",
                asset.ProposedFileName is null ? "无法生成目标文件名。" : "跳过重命名预览。"));
        }

        return inventory;
    }
}
