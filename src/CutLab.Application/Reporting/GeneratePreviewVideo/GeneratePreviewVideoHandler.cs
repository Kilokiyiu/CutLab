namespace CutLab.Application.Reporting.GeneratePreviewVideo;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record GeneratePreviewVideoCommand(
    ProjectId ProjectId,
    Guid SessionId,
    string OutputPath,
    double SecondsPerCut = 2.0,
    AssetType PreferredType = AssetType.Storyboard,
    string? VersionTagFilter = null);

public sealed record GeneratePreviewVideoResult(
    string OutputPath,
    int FrameCount,
    bool FfmpegAvailable);

public sealed class GeneratePreviewVideoHandler
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    private readonly IScanSessionRepository _sessionRepository;
    private readonly IPreviewVideoGenerator _previewVideoGenerator;

    public GeneratePreviewVideoHandler(
        IScanSessionRepository sessionRepository,
        IPreviewVideoGenerator previewVideoGenerator)
    {
        _sessionRepository = sessionRepository;
        _previewVideoGenerator = previewVideoGenerator;
    }

    public async Task<Result<GeneratePreviewVideoResult>> HandleAsync(
        GeneratePreviewVideoCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<GeneratePreviewVideoResult>("输出路径不能为空。");
        }

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<GeneratePreviewVideoResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<GeneratePreviewVideoResult>("扫描会话与项目不匹配。");
        }

        if (!await _previewVideoGenerator.IsAvailableAsync(cancellationToken))
        {
            return Result.Failure<GeneratePreviewVideoResult>(
                "未检测到 FFmpeg。请安装 FFmpeg 并确保 ffmpeg 在 PATH 中。");
        }

        var filteredSession = ScanAssetFilter.CreateFilteredView(session, command.VersionTagFilter);
        var frames = SelectFrames(filteredSession, command.PreferredType);
        if (frames.Count == 0)
        {
            return Result.Failure<GeneratePreviewVideoResult>("没有可用于合成的分镜/图片文件。");
        }

        var generateResult = await _previewVideoGenerator.GenerateAsync(
            frames,
            command.OutputPath,
            command.SecondsPerCut,
            cancellationToken);

        if (generateResult.IsFailure)
        {
            return Result.Failure<GeneratePreviewVideoResult>(generateResult.Error ?? "预览视频生成失败。");
        }

        return Result.Success(new GeneratePreviewVideoResult(
            command.OutputPath,
            frames.Count,
            true));
    }

    private static IReadOnlyList<PreviewFrameSource> SelectFrames(ScanSession session, AssetType preferredType)
    {
        var candidates = session.GetRecognized()
            .Where(asset => asset.ParsedCut is not null
                            && asset.AssetType is not null
                            && ImageExtensions.Contains(Path.GetExtension(asset.OriginalPath.Value)))
            .GroupBy(asset => asset.ParsedCut!.Value.Cut)
            .OrderBy(group => group.Key)
            .Select(group => PickBestAsset(group, preferredType))
            .Where(asset => asset is not null)
            .Cast<ProductionAsset>()
            .ToList();

        return candidates
            .Select((asset, index) => new PreviewFrameSource(asset.OriginalPath.Value, index + 1))
            .ToList();
    }

    private static ProductionAsset? PickBestAsset(IEnumerable<ProductionAsset> assets, AssetType preferredType)
    {
        var list = assets.ToList();
        return list.FirstOrDefault(asset => asset.AssetType == preferredType)
               ?? list.FirstOrDefault(asset => asset.AssetType == AssetType.Storyboard)
               ?? list.FirstOrDefault();
    }
}
