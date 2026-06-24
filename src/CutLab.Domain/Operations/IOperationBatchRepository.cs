namespace CutLab.Domain.Operations;

using CutLab.Domain.Projects;

public interface IOperationBatchRepository
{
    Task SaveAsync(OperationBatch batch, CancellationToken cancellationToken = default);

    Task<OperationBatch?> GetLastAppliedAsync(
        ProjectId projectId,
        CancellationToken cancellationToken = default);
}
