namespace CutLab.Infrastructure.Video;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;

public sealed class FfmpegPreviewVideoGenerator : IPreviewVideoGenerator
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Result<PreviewVideoGenerationResult>> GenerateAsync(
        IReadOnlyList<PreviewFrameSource> frames,
        string outputPath,
        double secondsPerFrame,
        CancellationToken cancellationToken = default)
    {
        if (frames.Count == 0)
        {
            return Result.Failure<PreviewVideoGenerationResult>("没有可合成的帧。");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var listPath = Path.Combine(Path.GetTempPath(), $"cutlab-preview-{Guid.NewGuid():N}.txt");
        try
        {
            await WriteConcatListAsync(listPath, frames, secondsPerFrame, cancellationToken);

            var arguments =
                $"-y -f concat -safe 0 -i \"{listPath}\" " +
                "-vf \"scale=1920:1080:force_original_aspect_ratio=decrease," +
                "pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=black\" " +
                "-pix_fmt yuv420p " +
                $"\"{outputPath}\"";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return Result.Failure<PreviewVideoGenerationResult>("无法启动 FFmpeg。");
            }

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? "FFmpeg 执行失败。" : stderr.Trim();
                return Result.Failure<PreviewVideoGenerationResult>(message);
            }

            return Result.Success(new PreviewVideoGenerationResult(outputPath, frames.Count));
        }
        finally
        {
            if (File.Exists(listPath))
            {
                File.Delete(listPath);
            }
        }
    }

    private static async Task WriteConcatListAsync(
        string listPath,
        IReadOnlyList<PreviewFrameSource> frames,
        double secondsPerFrame,
        CancellationToken cancellationToken)
    {
        var duration = secondsPerFrame.ToString("0.###", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();

        for (var index = 0; index < frames.Count; index++)
        {
            var framePath = frames[index].FilePath.Replace("'", "''", StringComparison.Ordinal);
            builder.AppendLine($"file '{framePath}'");
            builder.AppendLine($"duration {duration}");
        }

        var lastFramePath = frames[^1].FilePath.Replace("'", "''", StringComparison.Ordinal);
        builder.AppendLine($"file '{lastFramePath}'");

        await File.WriteAllTextAsync(listPath, builder.ToString(), cancellationToken);
    }
}
