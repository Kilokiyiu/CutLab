namespace CutLab.Application.Operations.InsertCut;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Cuts;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record InsertCutCommand(
    ProjectId ProjectId,
    Guid SessionId,
    int AfterCut,
    AssetType AssetType,
    bool UnrecognizedOnly,
    bool DryRun);

public sealed record InsertCutResult(
    bool DryRun,
    CutNumber InsertCutNumber,
    int ReadyCount,
    int RenamedCount,
    int SkippedCount,
    Guid? BatchId,
    IReadOnlyList<RenamePlanItem> Items);

public sealed partial class InsertCutHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly ICutRegistryRepository _registryRepository;
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly INamingService _namingService;
    private readonly IUnitOfWork _unitOfWork;

    public InsertCutHandler(
        IAnimationProjectRepository projectRepository,
        IScanSessionRepository sessionRepository,
        ICutRegistryRepository registryRepository,
        IOperationBatchRepository batchRepository,
        IFileSystemGateway fileSystemGateway,
        INamingService namingService,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _sessionRepository = sessionRepository;
        _registryRepository = registryRepository;
        _batchRepository = batchRepository;
        _fileSystemGateway = fileSystemGateway;
        _namingService = namingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InsertCutResult>> HandleAsync(
        InsertCutCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<InsertCutResult>("项目不存在。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<InsertCutResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<InsertCutResult>("扫描会话与项目不匹配。");
        }

        var (episode, scene) = ResolveEpisodeScene(session, project);

        var recognizedCuts = session.GetRecognized()
            .Where(asset => asset.ParsedCut is not null)
            .Select(asset => asset.ParsedCut!.Value)
            .Distinct()
            .ToList();

        var scopeFrom = recognizedCuts.Count > 0
            ? recognizedCuts.Min(cut => cut.Cut)
            : command.AfterCut;
        var scopeTo = recognizedCuts.Count > 0
            ? Math.Max(recognizedCuts.Max(cut => cut.Cut), command.AfterCut)
            : command.AfterCut;

        var scope = new CutScope(
            new EpisodeNumber(episode),
            scene,
            new CutNumber(episode, scene, scopeFrom),
            new CutNumber(episode, scene, scopeTo));

        var registry = await _registryRepository.GetByProjectAsync(command.ProjectId, scope, cancellationToken)
                       ?? CutRegistry.Create(command.ProjectId, scope);

        foreach (var cut in recognizedCuts)
        {
            registry.RegisterCut(cut);
        }

        var existingSuffixes = session.Assets
            .Where(asset => asset.ParsedCut is { } cut
                            && cut.Episode == episode
                            && cut.Scene == scene
                            && cut.Cut == command.AfterCut)
            .Select(asset => asset.ParsedCut!.Value.InsertSuffix)
            .Concat(registry.Cuts
                .Where(cut => cut.CutNumber.Cut == command.AfterCut)
                .Select(cut => cut.CutNumber.InsertSuffix));

        var insertResult = registry.CreateInsertCut(command.AfterCut, episode, scene, existingSuffixes);
        if (insertResult.IsFailure)
        {
            return Result.Failure<InsertCutResult>(insertResult.Error ?? "无法创建插卡。");
        }

        var insertCut = insertResult.Value;
        await _registryRepository.SaveAsync(registry, cancellationToken);

        var versionTag = project.DefaultVersionTag;
        var items = BuildRenameItems(session, project, insertCut, command, versionTag);
        var readyItems = items.Where(item => item.Status == RenamePlanStatus.Ready).ToList();

        if (command.DryRun)
        {
            return Result.Success(new InsertCutResult(
                true,
                insertCut,
                readyItems.Count,
                0,
                items.Count - readyItems.Count,
                null,
                items));
        }

        if (readyItems.Count == 0)
        {
            return Result.Failure<InsertCutResult>("没有可重命名的文件。");
        }

        var batch = OperationBatch.Create(command.ProjectId, BatchOperationType.Rename);
        foreach (var item in readyItems)
        {
            batch.AddEntry(OperationKind.Rename, item.SourcePath, item.TargetPath);
        }

        await _fileSystemGateway.ApplyOperationsAsync(batch, progress: null, cancellationToken);

        var completeResult = batch.Complete();
        if (completeResult.IsFailure)
        {
            return Result.Failure<InsertCutResult>(completeResult.Error ?? "插卡重命名未完成。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var renamedCount = batch.GetSuccessfulEntries().Count;
        return Result.Success(new InsertCutResult(
            false,
            insertCut,
            readyItems.Count,
            renamedCount,
            items.Count - renamedCount,
            batch.Id,
            items));
    }

    private static (int Episode, int Scene) ResolveEpisodeScene(ScanSession session, AnimationProject project)
    {
        var recognized = session.GetRecognized()
            .FirstOrDefault(asset => asset.ParsedCut is not null);

        if (recognized?.ParsedCut is { } cut)
        {
            return (cut.Episode, cut.Scene);
        }

        var sceneMatch = SceneTokenPattern().Match(project.NamingConvention.Template);
        var scene = sceneMatch.Success ? int.Parse(sceneMatch.Groups["value"].Value) : 1;
        return (project.Episode.Value, scene);
    }

    private List<RenamePlanItem> BuildRenameItems(
        ScanSession session,
        AnimationProject project,
        CutNumber insertCut,
        InsertCutCommand command,
        VersionTag? versionTag)
    {
        var items = new List<RenamePlanItem>();
        var targetPaths = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in session.Assets)
        {
            if (command.UnrecognizedOnly && asset.RecognitionStatus != RecognitionStatus.Unrecognized)
            {
                continue;
            }

            if (!command.UnrecognizedOnly
                && asset.RecognitionStatus == RecognitionStatus.Recognized
                && asset.ParsedCut is not null
                && asset.ParsedCut.Value.Cut != command.AfterCut)
            {
                continue;
            }

            var assetType = asset.AssetType ?? command.AssetType;
            var extension = Path.GetExtension(asset.OriginalPath.Value);
            var effectiveVersionTag = asset.VersionTag ?? versionTag;
            var naming = _namingService.GenerateFileName(
                project.NamingConvention,
                insertCut,
                assetType,
                extension,
                effectiveVersionTag);

            if (naming.IsFailure)
            {
                items.Add(new RenamePlanItem(
                    asset.Id,
                    asset.OriginalPath,
                    asset.OriginalPath,
                    new FileName(Path.GetFileName(asset.OriginalPath.Value)),
                    RenamePlanStatus.Skipped,
                    naming.Error ?? "无法生成目标文件名。"));
                continue;
            }

            var directory = Path.GetDirectoryName(asset.OriginalPath.Value) ?? string.Empty;
            var targetFullPath = Path.Combine(directory, naming.Value.Value);
            var targetPath = new FilePath(targetFullPath);

            if (string.Equals(asset.OriginalPath.Value, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new RenamePlanItem(
                    asset.Id,
                    asset.OriginalPath,
                    targetPath,
                    naming.Value,
                    RenamePlanStatus.AlreadyNamed,
                    "已是插卡命名。"));
                continue;
            }

            if (targetPaths.ContainsKey(targetFullPath) || File.Exists(targetFullPath))
            {
                items.Add(new RenamePlanItem(
                    asset.Id,
                    asset.OriginalPath,
                    targetPath,
                    naming.Value,
                    RenamePlanStatus.Conflict,
                    "目标文件名冲突。"));
                continue;
            }

            targetPaths[targetFullPath] = asset.Id;
            items.Add(new RenamePlanItem(
                asset.Id,
                asset.OriginalPath,
                targetPath,
                naming.Value,
                RenamePlanStatus.Ready,
                null));
        }

        return items;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"S(?<value>\d+)")]
    private static partial System.Text.RegularExpressions.Regex SceneTokenPattern();
}
