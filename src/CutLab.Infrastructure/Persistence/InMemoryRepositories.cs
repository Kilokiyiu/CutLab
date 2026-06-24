namespace CutLab.Infrastructure.Persistence;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Cuts;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

internal sealed class InMemoryCutRegistryRepository : ICutRegistryRepository
{
    private readonly Dictionary<string, CutRegistry> _store = [];

    public Task<CutRegistry?> GetByProjectAsync(
        ProjectId projectId,
        CutScope scope,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(BuildKey(projectId, scope), out var registry);
        return Task.FromResult(registry);
    }

    public Task SaveAsync(CutRegistry registry, CancellationToken cancellationToken = default)
    {
        _store[BuildKey(registry.ProjectId, registry.Scope)] = registry;
        return Task.CompletedTask;
    }

    private static string BuildKey(ProjectId projectId, CutScope scope) =>
        $"{projectId.Value}:{scope.Episode.Value}:{scope.Scene}:{scope.From.Cut}:{scope.To.Cut}";
}

internal sealed class InMemoryOperationBatchRepository : IOperationBatchRepository
{
    private readonly List<OperationBatch> _batches = [];

    public Task SaveAsync(OperationBatch batch, CancellationToken cancellationToken = default)
    {
        _batches.RemoveAll(b => b.Id == batch.Id);
        _batches.Add(batch);
        return Task.CompletedTask;
    }

    public Task<OperationBatch?> GetLastAppliedAsync(
        ProjectId projectId,
        CancellationToken cancellationToken = default)
    {
        var batch = _batches
            .Where(b => b.ProjectId == projectId && b.Status == BatchStatus.Applied)
            .OrderByDescending(b => b.AppliedAt)
            .FirstOrDefault();

        return Task.FromResult(batch);
    }
}

internal sealed class JsonUnitOfWork : Application.Common.Interfaces.IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
