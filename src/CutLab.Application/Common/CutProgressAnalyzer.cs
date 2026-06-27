namespace CutLab.Application.Common;

using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record CutProgressRow(
    string CutId,
    int Episode,
    int Scene,
    int Cut,
    string? InsertSuffix,
    bool HasStoryboard,
    bool HasKeyframe,
    bool HasInbetween,
    bool HasBackground,
    bool HasRender,
    int FileCount,
    string ProgressStatus);

public static class CutProgressAnalyzer
{
    public static IReadOnlyList<CutProgressRow> Analyze(ScanSession session)
    {
        return session.GetRecognized()
            .Where(asset => asset.ParsedCut is not null)
            .GroupBy(asset => asset.ParsedCut!.Value)
            .OrderBy(group => group.Key.Cut)
            .ThenBy(group => group.Key.InsertSuffix ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildRow(group.Key, group.ToList()))
            .ToList();
    }

    private static CutProgressRow BuildRow(CutNumber cut, IReadOnlyList<ProductionAsset> assets)
    {
        var types = assets
            .Where(asset => asset.AssetType is not null)
            .Select(asset => asset.AssetType!.Value)
            .ToHashSet();

        var hasStoryboard = types.Contains(AssetType.Storyboard);
        var hasKeyframe = types.Contains(AssetType.Keyframe);
        var hasInbetween = types.Contains(AssetType.Inbetween);
        var hasBackground = types.Contains(AssetType.Background);
        var hasRender = types.Contains(AssetType.Render);

        return new CutProgressRow(
            cut.ToString(),
            cut.Episode,
            cut.Scene,
            cut.Cut,
            cut.InsertSuffix,
            hasStoryboard,
            hasKeyframe,
            hasInbetween,
            hasBackground,
            hasRender,
            assets.Count,
            ResolveStatus(hasStoryboard, hasKeyframe, hasInbetween, hasBackground, hasRender));
    }

    private static string ResolveStatus(
        bool hasStoryboard,
        bool hasKeyframe,
        bool hasInbetween,
        bool hasBackground,
        bool hasRender)
    {
        if (hasRender)
        {
            return "已渲染";
        }

        if (hasKeyframe && hasInbetween)
        {
            return "动画完成";
        }

        if (hasKeyframe)
        {
            return "有原画";
        }

        if (hasStoryboard)
        {
            return "有分镜";
        }

        if (hasBackground)
        {
            return "有背景";
        }

        return "待开始";
    }
}
