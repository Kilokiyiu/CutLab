namespace CutLab.Domain.ValueObjects;

using CutLab.Domain.Common;

public sealed record FrameSequenceSettings(
    bool Enabled,
    string FileNamePattern,
    int MinFrame,
    int MaxFrame)
{
    public static FrameSequenceSettings Disabled { get; } = new(
        false,
        "C{CUT:03}_{FRAME:03}",
        1,
        99);

    public static Result<FrameSequenceSettings> Create(
        bool enabled,
        string fileNamePattern,
        int minFrame,
        int maxFrame)
    {
        if (!enabled)
        {
            return Result.Success(Disabled);
        }

        if (string.IsNullOrWhiteSpace(fileNamePattern))
        {
            return Result.Failure<FrameSequenceSettings>("帧序列文件名模板不能为空。");
        }

        if (!fileNamePattern.Contains("{FRAME", StringComparison.Ordinal))
        {
            return Result.Failure<FrameSequenceSettings>("帧序列模板必须包含 {FRAME} 占位符。");
        }

        if (minFrame < 1 || maxFrame < minFrame)
        {
            return Result.Failure<FrameSequenceSettings>("帧号范围无效。");
        }

        return Result.Success(new FrameSequenceSettings(
            true,
            fileNamePattern.Trim(),
            minFrame,
            maxFrame));
    }
}
