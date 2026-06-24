namespace CutLab.Domain.Cuts;

using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public interface ICutRegistryRepository
{
    Task<CutRegistry?> GetByProjectAsync(
        ProjectId projectId,
        CutScope scope,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CutRegistry registry, CancellationToken cancellationToken = default);
}
