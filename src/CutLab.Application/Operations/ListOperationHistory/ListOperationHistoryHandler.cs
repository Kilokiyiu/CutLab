namespace CutLab.Application.Operations.ListOperationHistory;

using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed record ListOperationHistoryQuery(ProjectId ProjectId, int Limit = 20);

public sealed record OperationHistoryItemDto(
    Guid BatchId,
    BatchOperationType OperationType,
    BatchStatus Status,
    DateTimeOffset AppliedAt,
    int EntryCount,
    int SuccessCount,
    string Summary);

public sealed class ListOperationHistoryHandler
{
    private readonly IOperationBatchRepository _batchRepository;

    public ListOperationHistoryHandler(IOperationBatchRepository batchRepository)
    {
        _batchRepository = batchRepository;
    }

    public async Task<IReadOnlyList<OperationHistoryItemDto>> HandleAsync(
        ListOperationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var batches = await _batchRepository.ListRecentAsync(
            query.ProjectId,
            query.Limit,
            cancellationToken);

        return batches
            .Select(batch => new OperationHistoryItemDto(
                batch.Id,
                batch.OperationType,
                batch.Status,
                batch.AppliedAt ?? DateTimeOffset.MinValue,
                batch.Entries.Count,
                batch.GetSuccessfulEntries().Count,
                FormatSummary(batch)))
            .ToList();
    }

    private static string FormatSummary(OperationBatch batch)
    {
        var typeLabel = batch.OperationType switch
        {
            BatchOperationType.Rename => "重命名",
            BatchOperationType.Move => "归档移动",
            BatchOperationType.CreateDirectories => "创建目录",
            _ => batch.OperationType.ToString()
        };

        var statusLabel = batch.Status switch
        {
            BatchStatus.Applied => "可撤销",
            BatchStatus.Undone => "已撤销",
            _ => batch.Status.ToString()
        };

        var timeLabel = batch.AppliedAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "-";
        return $"{timeLabel} · {typeLabel} · {batch.GetSuccessfulEntries().Count} 项 · {statusLabel}";
    }
}
