namespace CutLab.Application.Operations.ExecuteArchive;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record ExecuteArchiveCommand(
    ProjectId ProjectId,
    Guid SessionId,
    ArchiveExecutionMode Mode,
    bool DryRun);

public sealed record ExecuteArchiveResult(
    bool DryRun,
    ArchiveExecutionMode Mode,
    int ReadyCount,
    int ExecutedCount,
    int SkippedCount,
    Guid? BatchId,
    IReadOnlyList<ArchivePlanItem> Items);

public sealed class ExecuteArchiveHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly IArchivePathResolver _archivePathResolver;
    private readonly IUnitOfWork _unitOfWork;

    public ExecuteArchiveHandler(
        IAnimationProjectRepository projectRepository,
        IScanSessionRepository sessionRepository,
        IOperationBatchRepository batchRepository,
        IFileSystemGateway fileSystemGateway,
        IArchivePathResolver archivePathResolver,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _sessionRepository = sessionRepository;
        _batchRepository = batchRepository;
        _fileSystemGateway = fileSystemGateway;
        _archivePathResolver = archivePathResolver;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExecuteArchiveResult>> HandleAsync(
        ExecuteArchiveCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ExecuteArchiveResult>("项目不存在。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ExecuteArchiveResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<ExecuteArchiveResult>("扫描会话与项目不匹配。");
        }

        var items = ArchivePlanBuilder.Build(project, session, _archivePathResolver, command.Mode);
        var readyItems = items.Where(item => item.Status == ArchivePlanStatus.Ready).ToList();

        if (command.DryRun)
        {
            return Result.Success(new ExecuteArchiveResult(
                true,
                command.Mode,
                readyItems.Count,
                0,
                items.Count - readyItems.Count,
                null,
                items));
        }

        if (readyItems.Count == 0)
        {
            return Result.Failure<ExecuteArchiveResult>("没有可执行的归档项。");
        }

        var batch = OperationBatch.Create(
            command.ProjectId,
            command.Mode == ArchiveExecutionMode.CreateDirectoriesOnly
                ? BatchOperationType.CreateDirectories
                : BatchOperationType.Move);

        foreach (var item in readyItems)
        {
            if (item.OperationKind == ArchiveOperationKind.CreateDirectory)
            {
                batch.AddEntry(OperationKind.CreateDirectory, item.TargetPath, item.TargetPath);
            }
            else if (item.SourcePath is { } sourcePath)
            {
                batch.AddEntry(OperationKind.Move, sourcePath, item.TargetPath);
            }
        }

        await _fileSystemGateway.ApplyOperationsAsync(batch, progress: null, cancellationToken);

        var completeResult = batch.Complete();
        if (completeResult.IsFailure)
        {
            return Result.Failure<ExecuteArchiveResult>(completeResult.Error ?? "归档未完成。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var executedCount = batch.GetSuccessfulEntries().Count;
        return Result.Success(new ExecuteArchiveResult(
            false,
            command.Mode,
            readyItems.Count,
            executedCount,
            items.Count - executedCount,
            batch.Id,
            items));
    }
}
