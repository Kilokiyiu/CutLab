namespace CutLab.Application.Operations.ExecuteRename;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record ExecuteRenameCommand(
    ProjectId ProjectId,
    Guid SessionId,
    bool DryRun,
    ConflictResolutionStrategy ConflictStrategy = ConflictResolutionStrategy.Fail);

public sealed record ExecuteRenameResult(
    bool DryRun,
    int ReadyCount,
    int RenamedCount,
    int SkippedCount,
    Guid? BatchId,
    IReadOnlyList<RenamePlanItem> Items);

public sealed class ExecuteRenameHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly IUnitOfWork _unitOfWork;

    public ExecuteRenameHandler(
        IAnimationProjectRepository projectRepository,
        IScanSessionRepository sessionRepository,
        IOperationBatchRepository batchRepository,
        IFileSystemGateway fileSystemGateway,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _sessionRepository = sessionRepository;
        _batchRepository = batchRepository;
        _fileSystemGateway = fileSystemGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExecuteRenameResult>> HandleAsync(
        ExecuteRenameCommand command,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ExecuteRenameResult>("项目不存在。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ExecuteRenameResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<ExecuteRenameResult>("扫描会话与项目不匹配。");
        }

        var items = RenamePlanBuilder.Build(session, command.ConflictStrategy);
        var readyItems = items.Where(item => item.Status == RenamePlanStatus.Ready).ToList();

        if (command.DryRun)
        {
            return Result.Success(new ExecuteRenameResult(
                true,
                readyItems.Count,
                0,
                items.Count - readyItems.Count,
                null,
                items));
        }

        if (readyItems.Count == 0)
        {
            return Result.Failure<ExecuteRenameResult>("没有可执行的 rename 项。");
        }

        var batch = OperationBatch.Create(command.ProjectId, BatchOperationType.Rename);
        foreach (var item in readyItems)
        {
            batch.AddEntry(OperationKind.Rename, item.SourcePath, item.TargetPath);
        }

        await _fileSystemGateway.ApplyOperationsAsync(batch, progress, cancellationToken);

        var completeResult = batch.Complete();
        if (completeResult.IsFailure)
        {
            return Result.Failure<ExecuteRenameResult>(completeResult.Error ?? "重命名未完成。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var renamedCount = batch.GetSuccessfulEntries().Count;
        return Result.Success(new ExecuteRenameResult(
            false,
            readyItems.Count,
            renamedCount,
            items.Count - renamedCount,
            batch.Id,
            items));
    }
}
