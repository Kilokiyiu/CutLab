namespace CutLab.Application.Operations.BatchAutoNumberUnrecognized;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record BatchAutoNumberUnrecognizedCommand(
    ProjectId ProjectId,
    Guid SessionId,
    int StartCutNumber,
    AssetType DefaultAssetType,
    bool DryRun);

public sealed record BatchAutoNumberUnrecognizedResult(
    bool DryRun,
    int StartCutNumber,
    int EndCutNumber,
    int ReadyCount,
    int RenamedCount,
    int SkippedCount,
    Guid? BatchId,
    IReadOnlyList<RenamePlanItem> Items);

public sealed partial class BatchAutoNumberUnrecognizedHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IOperationBatchRepository _batchRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly INamingService _namingService;
    private readonly IUnitOfWork _unitOfWork;

    public BatchAutoNumberUnrecognizedHandler(
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

    public async Task<Result<BatchAutoNumberUnrecognizedResult>> HandleAsync(
        BatchAutoNumberUnrecognizedCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.StartCutNumber < 1)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("起始卡号必须大于 0。");
        }

        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("项目不存在。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("扫描会话与项目不匹配。");
        }

        var unrecognized = session.GetUnrecognized()
            .OrderBy(asset => Path.GetFileName(asset.OriginalPath.Value), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unrecognized.Count == 0)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("没有未识别的文件。");
        }

        var (episode, scene) = EpisodeSceneResolver.Resolve(session, project);
        var versionTag = project.DefaultVersionTag;
        var items = BuildRenameItems(
            session,
            unrecognized,
            project,
            episode,
            scene,
            command.StartCutNumber,
            command.DefaultAssetType,
            versionTag);

        var readyItems = items.Where(item => item.Status == RenamePlanStatus.Ready).ToList();
        var endCut = items
            .Where(item => item.Status is RenamePlanStatus.Ready or RenamePlanStatus.AlreadyNamed)
            .Select(item => ExtractCutNumber(item.ProposedFileName.Value))
            .Where(cut => cut.HasValue)
            .Select(cut => cut!.Value)
            .DefaultIfEmpty(command.StartCutNumber)
            .Max();

        if (command.DryRun)
        {
            return Result.Success(new BatchAutoNumberUnrecognizedResult(
                true,
                command.StartCutNumber,
                endCut,
                readyItems.Count,
                0,
                items.Count - readyItems.Count,
                null,
                items));
        }

        if (readyItems.Count == 0)
        {
            return Result.Failure<BatchAutoNumberUnrecognizedResult>("没有可重命名的文件。");
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
            return Result.Failure<BatchAutoNumberUnrecognizedResult>(
                completeResult.Error ?? "自动编号重命名未完成。");
        }

        await _batchRepository.SaveAsync(batch, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var renamedCount = batch.GetSuccessfulEntries().Count;
        return Result.Success(new BatchAutoNumberUnrecognizedResult(
            false,
            command.StartCutNumber,
            endCut,
            readyItems.Count,
            renamedCount,
            items.Count - renamedCount,
            batch.Id,
            items));
    }

    private List<RenamePlanItem> BuildRenameItems(
        ScanSession session,
        IReadOnlyList<ProductionAsset> unrecognized,
        AnimationProject project,
        int episode,
        int scene,
        int startCutNumber,
        AssetType defaultAssetType,
        VersionTag? versionTag)
    {
        var items = new List<RenamePlanItem>();
        var targetPaths = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var occupiedCuts = session.GetRecognized()
            .Where(asset => asset.ParsedCut is { } cut
                            && cut.Episode == episode
                            && cut.Scene == scene)
            .Select(asset => asset.ParsedCut!.Value.Cut)
            .ToHashSet();
        var nextCut = startCutNumber;

        foreach (var asset in unrecognized)
        {
            while (occupiedCuts.Contains(nextCut))
            {
                nextCut++;
            }

            var assignedCut = new CutNumber(episode, scene, nextCut);
            occupiedCuts.Add(nextCut);
            nextCut++;

            var assetType = asset.AssetType ?? defaultAssetType;
            var extension = Path.GetExtension(asset.OriginalPath.Value);
            var effectiveVersionTag = asset.VersionTag ?? versionTag;
            var naming = _namingService.GenerateFileName(
                project.NamingConvention,
                assignedCut,
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
                    "已是规范命名。"));
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

    private static int? ExtractCutNumber(string fileName)
    {
        var match = CutTokenPattern().Match(Path.GetFileNameWithoutExtension(fileName));
        return match.Success ? int.Parse(match.Groups["cut"].Value) : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"C(?<cut>\d{3})")]
    private static partial System.Text.RegularExpressions.Regex CutTokenPattern();
}
