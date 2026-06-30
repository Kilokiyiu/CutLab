namespace CutLab.App.ViewModels;

using CutLab.Application.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.ValueObjects;

public sealed class ConflictStrategyOption
{
    public ConflictStrategyOption(ConflictResolutionStrategy strategy, string label)
    {
        Strategy = strategy;
        Label = label;
    }

    public ConflictResolutionStrategy Strategy { get; }

    public string Label { get; }

    public static IReadOnlyList<ConflictStrategyOption> All { get; } =
    [
        new(ConflictResolutionStrategy.Fail, "遇冲突报错"),
        new(ConflictResolutionStrategy.Skip, "跳过冲突项"),
        new(ConflictResolutionStrategy.AutoSuffix, "自动加后缀 _1")
    ];
}

public sealed class OperationHistoryItemViewModel
{
    public OperationHistoryItemViewModel(
        Guid batchId,
        string summary,
        BatchStatus status,
        bool canUndo)
    {
        BatchId = batchId;
        Summary = summary;
        Status = status;
        CanUndo = canUndo;
    }

    public Guid BatchId { get; }

    public string Summary { get; }

    public BatchStatus Status { get; }

    public bool CanUndo { get; }

    public override string ToString() => Summary;
}
