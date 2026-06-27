namespace CutLab.Application.Scanning.GetScanPreview;

using CutLab.Application.Common;
using CutLab.Domain.Common;
using CutLab.Domain.Scanning;

public sealed record GetScanPreviewQuery(Guid SessionId);

public sealed record ScanPreviewDto(
    Guid SessionId,
    int TotalFiles,
    int ReadyCount,
    int ConflictCount,
    int AlreadyNamedCount,
    IReadOnlyList<RenamePlanItem> Items);

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

        var items = RenamePlanBuilder.Build(session);
        return Result.Success(new ScanPreviewDto(
            session.Id,
            session.Assets.Count,
            items.Count(item => item.Status == RenamePlanStatus.Ready),
            items.Count(item => item.Status == RenamePlanStatus.Conflict),
            items.Count(item => item.Status == RenamePlanStatus.AlreadyNamed),
            items));
    }
}
