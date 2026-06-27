namespace CutLab.Application.Operations.UndoLastOperation;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;

public sealed record UndoLastOperationCommand(ProjectId ProjectId);

public sealed record UndoLastOperationResult(Guid BatchId, int RevertedCount);

public sealed class UndoLastOperationHandler
{
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly IUnitOfWork _unitOfWork;

    public UndoLastOperationHandler(
        IOperationBatchRepository batchRepository,
        IFileSystemGateway fileSystemGateway,
        IUnitOfWork unitOfWork)
    {
        _batchRepository = batchRepository;
        _fileSystemGateway = fileSystemGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UndoLastOperationResult>> HandleAsync(
        UndoLastOperationCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetLastAppliedAsync(command.ProjectId, cancellationToken);
        if (batch is null || !batch.CanUndo())
        {
            return Result.Failure<UndoLastOperationResult>("没有可撤销的操作。");
        }

        await _fileSystemGateway.RevertOperationsAsync(batch, cancellationToken);

        var undoResult = batch.Undo();
        if (undoResult.IsFailure)
        {
            return Result.Failure<UndoLastOperationResult>(undoResult.Error ?? "撤销失败。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new UndoLastOperationResult(
            batch.Id,
            batch.GetSuccessfulEntries().Count));
    }
}
