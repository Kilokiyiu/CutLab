namespace CutLab.Domain.Operations;

using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed class FileOperationEntry : Entity<Guid>
{
    internal FileOperationEntry(OperationKind kind, FilePath sourcePath, FilePath targetPath)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        SourcePath = sourcePath;
        TargetPath = targetPath;
    }

    public OperationKind Kind { get; }

    public FilePath SourcePath { get; }

    public FilePath TargetPath { get; }

    public bool Success { get; private set; }

    public void MarkSuccess() => Success = true;

    public void MarkFailed() => Success = false;
}

public sealed class OperationBatch : AggregateRoot<Guid>
{
    private readonly List<FileOperationEntry> _entries = [];

    private OperationBatch(
        Guid id,
        ProjectId projectId,
        BatchOperationType operationType)
    {
        Id = id;
        ProjectId = projectId;
        OperationType = operationType;
        Status = BatchStatus.Pending;
    }

    public ProjectId ProjectId { get; }

    public BatchOperationType OperationType { get; }

    public BatchStatus Status { get; private set; }

    public DateTimeOffset? AppliedAt { get; private set; }

    public IReadOnlyList<FileOperationEntry> Entries => _entries.AsReadOnly();

    public static OperationBatch Create(ProjectId projectId, BatchOperationType operationType) =>
        new(Guid.NewGuid(), projectId, operationType);

    public FileOperationEntry AddEntry(OperationKind kind, FilePath sourcePath, FilePath targetPath)
    {
        var entry = new FileOperationEntry(kind, sourcePath, targetPath);
        _entries.Add(entry);
        return entry;
    }

    public Result Complete()
    {
        if (Status != BatchStatus.Pending)
        {
            return Result.Failure("仅待执行批次可以完成。");
        }

        if (!_entries.Any(entry => entry.Success))
        {
            return Result.Failure("没有成功执行的操作。");
        }

        Status = BatchStatus.Applied;
        AppliedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Undo()
    {
        if (Status != BatchStatus.Applied)
        {
            return Result.Failure("仅已应用批次可以撤销。");
        }

        Status = BatchStatus.Undone;
        return Result.Success();
    }

    public bool CanUndo() => Status == BatchStatus.Applied;

    public IReadOnlyList<FileOperationEntry> GetSuccessfulEntries() =>
        _entries.Where(entry => entry.Success).ToList();
}
