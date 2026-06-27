namespace CutLab.Application.Scanning.GetScanPreview;

using CutLab.Application.Common;
using CutLab.Domain.Common;
using CutLab.Domain.Scanning;

public sealed record GetScanPreviewQuery(Guid SessionId, string? VersionTagFilter = null);

public sealed record ScanPreviewDto(
    Guid SessionId,
    int TotalFiles,
    int ReadyCount,
    int ConflictCount,
    int AlreadyNamedCount,
    int UnrecognizedCount,
    IReadOnlyList<ScanInventoryItem> Items);

public sealed class GetScanPreviewHandler
{
    private readonly IScanSessionRepository _sessionRepository;

    public GetScanPreviewHandler(IScanSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<Result<ScanPreviewDto>> HandleAsync(
        GetScanPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ScanPreviewDto>("扫描会话不存在。");
        }

        var filteredSession = ScanAssetFilter.CreateFilteredView(session, query.VersionTagFilter);
        var items = ScanInventoryBuilder.Build(filteredSession);
        var renameItems = RenamePlanBuilder.Build(filteredSession);

        return Result.Success(new ScanPreviewDto(
            session.Id,
            filteredSession.Assets.Count,
            renameItems.Count(item => item.Status == RenamePlanStatus.Ready),
            renameItems.Count(item => item.Status == RenamePlanStatus.Conflict),
            renameItems.Count(item => item.Status == RenamePlanStatus.AlreadyNamed),
            filteredSession.GetUnrecognized().Count,
            items));
    }
}
