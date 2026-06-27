namespace CutLab.Application.Common.Interfaces;

using CutLab.Domain.Common;

public sealed record PreviewFrameSource(string FilePath, int Order);

public sealed record PreviewVideoGenerationResult(string OutputPath, int FrameCount);

public interface IPreviewVideoGenerator
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<Result<PreviewVideoGenerationResult>> GenerateAsync(
        IReadOnlyList<PreviewFrameSource> frames,
        string outputPath,
        double secondsPerFrame,
        CancellationToken cancellationToken = default);
}
