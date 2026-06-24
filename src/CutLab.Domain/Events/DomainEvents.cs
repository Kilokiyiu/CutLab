namespace CutLab.Domain.Events;

using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record ScanSessionCompleted(
    Guid SessionId,
    ProjectId ProjectId,
    int TotalFiles,
    int RecognizedCount,
    int UnrecognizedCount) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record OperationBatchApplied(
    Guid BatchId,
    ProjectId ProjectId,
    int EntryCount) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed record MissingCutsDetected(
    ProjectId ProjectId,
    int MissingCount) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
