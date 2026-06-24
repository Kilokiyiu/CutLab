namespace CutLab.Application.Reporting.GetMissingCuts;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Cuts;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed record GetMissingCutsQuery(ProjectId ProjectId, CutScope Scope);

public sealed record MissingCutsReportDto(
    IReadOnlyList<MissingCut> MissingCuts,
    int TotalExpected,
    int RegisteredCount);

public sealed class GetMissingCutsHandler
{
    private readonly ICutRegistryRepository _registryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GetMissingCutsHandler(ICutRegistryRepository registryRepository, IUnitOfWork unitOfWork)
    {
        _registryRepository = registryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MissingCutsReportDto>> HandleAsync(
        GetMissingCutsQuery query,
        CancellationToken cancellationToken = default)
    {
        var registry = await _registryRepository.GetByProjectAsync(
                           query.ProjectId,
                           query.Scope,
                           cancellationToken)
                       ?? CutRegistry.Create(query.ProjectId, query.Scope);

        var missing = registry.DetectGaps();
        await _registryRepository.SaveAsync(registry, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var totalExpected = query.Scope.To.Cut - query.Scope.From.Cut + 1;
        return Result.Success(new MissingCutsReportDto(missing, totalExpected, registry.Cuts.Count));
    }
}
