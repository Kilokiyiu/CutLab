namespace CutLab.Application.Common;

using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public static class ScanAssetFilter
{
    public static IEnumerable<ProductionAsset> ApplyVersionFilter(
        IEnumerable<ProductionAsset> assets,
        string? versionTagFilter) =>
        string.IsNullOrWhiteSpace(versionTagFilter)
            ? assets
            : assets.Where(asset => VersionTagParser.MatchesFilter(asset.VersionTag, versionTagFilter));

    public static ScanSession CreateFilteredView(ScanSession session, string? versionTagFilter)
    {
        if (string.IsNullOrWhiteSpace(versionTagFilter))
        {
            return session;
        }

        var filtered = ApplyVersionFilter(session.Assets, versionTagFilter).ToList();
        var view = ScanSession.Create(session.ProjectId, session.SourcePath);
        foreach (var asset in filtered)
        {
            view.AddDiscoveredAsset(
                asset.OriginalPath,
                asset.ParsedCut,
                asset.AssetType,
                asset.ProposedFileName,
                asset.RecognitionStatus,
                asset.VersionTag);
        }

        return view;
    }
}
