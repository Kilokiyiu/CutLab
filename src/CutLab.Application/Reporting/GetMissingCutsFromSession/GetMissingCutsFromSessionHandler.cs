namespace CutLab.Application.Reporting.GetMissingCutsFromSession;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Cuts;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record GetMissingCutsFromSessionQuery(ProjectId ProjectId, Guid SessionId);

public sealed record MissingCutsFromSessionDto(
    CutScope Scope,
    IReadOnlyList<MissingCut> MissingCuts,
    int RegisteredCount,
    int TotalExpected);

public sealed class GetMissingCutsFromSessionHandler
{
    private readonly IScanSessionRepository _sessionRepository;
    private readonly ICutRegistryRepository _registryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GetMissingCutsFromSessionHandler(
        IScanSessionRepository sessionRepository,
        ICutRegistryRepository registryRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _registryRepository = registryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MissingCutsFromSessionDto>> HandleAsync(
        GetMissingCutsFromSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<MissingCutsFromSessionDto>("扫描会话不存在。");
        }

        if (session.ProjectId != query.ProjectId)
        {
            return Result.Failure<MissingCutsFromSessionDto>("扫描会话与项目不匹配。");
        }

        var recognizedCuts = session.GetRecognized()
            .Where(asset => asset.ParsedCut is not null)
            .Select(asset => asset.ParsedCut!.Value)
            .Distinct()
            .OrderBy(cut => cut.Cut)
            .ToList();

        if (recognizedCuts.Count == 0)
        {
            return Result.Failure<MissingCutsFromSessionDto>("扫描结果中没有可识别的卡号。");
        }

        var first = recognizedCuts[0];
        var scope = new CutScope(
            new EpisodeNumber(first.Episode),
            first.Scene,
            new CutNumber(first.Episode, first.Scene, recognizedCuts.Min(c => c.Cut)),
            new CutNumber(first.Episode, first.Scene, recognizedCuts.Max(c => c.Cut)));

        var registry = await _registryRepository.GetByProjectAsync(query.ProjectId, scope, cancellationToken)
                       ?? CutRegistry.Create(query.ProjectId, scope);

        foreach (var cut in recognizedCuts)
        {
            registry.RegisterCut(cut);
        }

        var missing = registry.DetectGaps();
        await _registryRepository.SaveAsync(registry, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new MissingCutsFromSessionDto(
            scope,
            missing,
            registry.Cuts.Count,
            scope.To.Cut - scope.From.Cut + 1));
    }
}
