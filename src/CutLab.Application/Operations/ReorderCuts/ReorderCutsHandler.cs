namespace CutLab.Application.Operations.ReorderCuts;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record ReorderCutsCommand(
    ProjectId ProjectId,
    Guid SessionId,
    int MovedCut,
    int TargetIndex,
    bool DryRun,
    ConflictResolutionStrategy ConflictStrategy = ConflictResolutionStrategy.Fail);

public sealed record ReorderCutsResult(
    bool DryRun,
    int MovedCut,
    int TargetIndex,
    int ReadyCount,
    int RenamedCount,
    int SkippedCount,
    Guid? BatchId,
    IReadOnlyList<RenamePlanItem> Items);

public sealed class ReorderCutsHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly INamingService _namingService;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderCutsHandler(
        IAnimationProjectRepository projectRepository,
        IScanSessionRepository sessionRepository,
        IOperationBatchRepository batchRepository,
        IFileSystemGateway fileSystemGateway,
        INamingService namingService,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _sessionRepository = sessionRepository;
        _batchRepository = batchRepository;
        _fileSystemGateway = fileSystemGateway;
        _namingService = namingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ReorderCutsResult>> HandleAsync(
        ReorderCutsCommand command,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ReorderCutsResult>("项目不存在。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ReorderCutsResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<ReorderCutsResult>("扫描会话与项目不匹配。");
        }

        var (episode, scene) = EpisodeSceneResolver.Resolve(session, project);
        var mapResult = CutSequenceReorderPlanner.BuildRenumberMap(
            session,
            episode,
            scene,
            command.MovedCut,
            command.TargetIndex);

        if (mapResult.IsFailure || mapResult.Value is null)
        {
            return Result.Failure<ReorderCutsResult>(mapResult.Error ?? "无法计算卡号重排。");
        }

        var versionTag = project.DefaultVersionTag;
        var items = BuildRenameItems(session, project, mapResult.Value, versionTag, command.ConflictStrategy);
        var readyItems = items.Where(item => item.Status == RenamePlanStatus.Ready).ToList();

        if (command.DryRun)
        {
            return Result.Success(new ReorderCutsResult(
                true,
                command.MovedCut,
                command.TargetIndex,
                readyItems.Count,
                0,
                items.Count - readyItems.Count,
                null,
                items));
        }

        if (readyItems.Count == 0)
        {
            return Result.Failure<ReorderCutsResult>("没有可重命名的文件。");
        }

        var batch = OperationBatch.Create(command.ProjectId, BatchOperationType.Rename);
        AddStagedRenameEntries(batch, readyItems);

        await _fileSystemGateway.ApplyOperationsAsync(batch, progress, cancellationToken);

        var completeResult = batch.Complete();
        if (completeResult.IsFailure)
        {
            return Result.Failure<ReorderCutsResult>(completeResult.Error ?? "卡号重排序未完成。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new ReorderCutsResult(
            false,
            command.MovedCut,
            command.TargetIndex,
            readyItems.Count,
            readyItems.Count,
            items.Count - readyItems.Count,
            batch.Id,
            items));
    }

    private List<RenamePlanItem> BuildRenameItems(
        ScanSession session,
        AnimationProject project,
        IReadOnlyDictionary<CutNumber, CutNumber> renumberMap,
        VersionTag? versionTag,
        ConflictResolutionStrategy conflictStrategy)
    {
        var candidates = new List<RenameCandidate>();

        foreach (var asset in session.Assets)
        {
            if (asset.ParsedCut is not { } parsedCut
                || !renumberMap.TryGetValue(parsedCut, out var newCut))
            {
                continue;
            }

            var assetType = asset.AssetType ?? AssetType.Keyframe;
            var extension = Path.GetExtension(asset.OriginalPath.Value);
            var effectiveVersionTag = asset.VersionTag ?? versionTag;
            var naming = _namingService.GenerateFileName(
                project.NamingConvention,
                newCut,
                assetType,
                extension,
                effectiveVersionTag);

            if (naming.IsFailure)
            {
                candidates.Add(new RenameCandidate(
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
            var samePath = string.Equals(
                asset.OriginalPath.Value,
                targetFullPath,
                StringComparison.OrdinalIgnoreCase);

            candidates.Add(new RenameCandidate(
                asset.Id,
                asset.OriginalPath,
                targetPath,
                naming.Value,
                samePath ? RenamePlanStatus.AlreadyNamed : RenamePlanStatus.Ready,
                samePath ? "已是目标卡号。" : null));
        }

        var movingSourcePaths = new HashSet<string>(
            candidates
                .Where(candidate => candidate.Status == RenamePlanStatus.Ready)
                .Select(candidate => candidate.SourcePath.Value),
            StringComparer.OrdinalIgnoreCase);

        var items = new List<RenamePlanItem>();
        var targetPaths = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (candidate.Status is RenamePlanStatus.Skipped or RenamePlanStatus.AlreadyNamed)
            {
                items.Add(candidate.ToPlanItem());
                continue;
            }

            var targetFullPath = candidate.TargetPath.Value;
            var resolution = TargetPathConflictResolver.ResolveRename(
                targetFullPath,
                candidate.ProposedFileName,
                candidate.AssetId,
                targetPaths,
                conflictStrategy,
                movingSourcePaths);

            items.Add(new RenamePlanItem(
                candidate.AssetId,
                candidate.SourcePath,
                resolution.TargetPath,
                resolution.ProposedFileName,
                resolution.Status,
                resolution.Message));
        }

        return items;
    }

    private sealed record RenameCandidate(
        Guid AssetId,
        FilePath SourcePath,
        FilePath TargetPath,
        FileName ProposedFileName,
        RenamePlanStatus Status,
        string? Message)
    {
        public RenamePlanItem ToPlanItem() =>
            new(AssetId, SourcePath, TargetPath, ProposedFileName, Status, Message);
    }

    private static void AddStagedRenameEntries(OperationBatch batch, IReadOnlyList<RenamePlanItem> readyItems)
    {
        var staging = new List<(FilePath TempPath, FilePath TargetPath)>();
        foreach (var item in readyItems)
        {
            var directory = Path.GetDirectoryName(item.SourcePath.Value) ?? string.Empty;
            var extension = Path.GetExtension(item.SourcePath.Value);
            var tempPath = new FilePath(Path.Combine(directory, $".cutlab-{item.AssetId:N}{extension}"));
            batch.AddEntry(OperationKind.Rename, item.SourcePath, tempPath);
            staging.Add((tempPath, item.TargetPath));
        }

        foreach (var (tempPath, targetPath) in staging)
        {
            batch.AddEntry(OperationKind.Rename, tempPath, targetPath);
        }
    }
}
