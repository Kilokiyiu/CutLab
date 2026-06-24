namespace CutLab.Application.Common.Interfaces;

using CutLab.Domain.Operations;
using CutLab.Domain.ValueObjects;

public interface IFileSystemGateway
{
    IAsyncEnumerable<FilePath> EnumerateFilesAsync(
        WorkspacePath root,
        bool recursive,
        CancellationToken cancellationToken = default);

    Task ApplyOperationsAsync(
        OperationBatch batch,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task RevertOperationsAsync(
        OperationBatch batch,
        CancellationToken cancellationToken = default);
}

public sealed record OperationProgress(int Completed, int Total, string? CurrentFile);

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
