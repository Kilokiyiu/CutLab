namespace CutLab.Domain.Services;

using CutLab.Domain.ValueObjects;

public interface IFrameSequenceAnalyzer
{
    FrameSequenceAnalysisResult Analyze(
        FrameSequenceSettings settings,
        IReadOnlyList<ParsedFrameFile> files);
}

public sealed class FrameSequenceAnalyzer : IFrameSequenceAnalyzer
{
    public FrameSequenceAnalysisResult Analyze(
        FrameSequenceSettings settings,
        IReadOnlyList<ParsedFrameFile> files)
    {
        if (!settings.Enabled)
        {
            return new FrameSequenceAnalysisResult(
                false,
                0,
                [],
                [],
                [],
                []);
        }

        var parsed = files.Where(file => file.FrameNumber > 0).ToList();
        var missingFrames = new List<MissingFrameIssue>();
        var duplicateFrames = new List<DuplicateFrameIssue>();
        var crossCutFrames = new List<CrossCutFrameIssue>();
        var orphanFrames = new List<OrphanFrameIssue>();

        foreach (var file in files)
        {
            if (file.FrameNumber <= 0)
            {
                continue;
            }

            if (file.CutNumber is null)
            {
                orphanFrames.Add(new OrphanFrameIssue(
                    file.Path.Value,
                    file.FrameNumber,
                    "已识别帧号，但无法关联镜头卡号。"));
            }
        }

        var byCut = parsed
            .Where(file => file.CutNumber is not null)
            .GroupBy(file => file.CutNumber!.Value)
            .ToList();

        foreach (var cutGroup in byCut)
        {
            var framesByNumber = cutGroup
                .GroupBy(file => file.FrameNumber)
                .ToDictionary(group => group.Key, group => group.ToList());

            var observedFrames = framesByNumber.Keys.OrderBy(frame => frame).ToList();
            var rangeFrom = Math.Max(settings.MinFrame, observedFrames.Min());
            var rangeTo = Math.Min(settings.MaxFrame, observedFrames.Max());

            for (var frame = rangeFrom; frame <= rangeTo; frame++)
            {
                if (!framesByNumber.ContainsKey(frame))
                {
                    missingFrames.Add(new MissingFrameIssue(
                        cutGroup.Key,
                        frame,
                        $"C{cutGroup.Key.Cut:D3} 在帧 {rangeFrom:D3}–{rangeTo:D3} 范围内缺少帧 {frame:D3}。"));
                }
            }

            foreach (var frameGroup in framesByNumber)
            {
                if (frameGroup.Value.Count <= 1)
                {
                    continue;
                }

                duplicateFrames.Add(new DuplicateFrameIssue(
                    cutGroup.Key,
                    frameGroup.Key,
                    frameGroup.Value.Select(file => file.Path.Value).ToList(),
                    $"C{cutGroup.Key.Cut:D3} 帧 {frameGroup.Key:D3} 存在 {frameGroup.Value.Count} 个文件。"));
            }
        }

        var byFrameNumber = parsed
            .Where(file => file.CutNumber is not null)
            .GroupBy(file => file.FrameNumber);

        foreach (var frameGroup in byFrameNumber)
        {
            var cuts = frameGroup
                .Select(file => file.CutNumber!.Value)
                .Distinct()
                .ToList();

            if (cuts.Count <= 1)
            {
                continue;
            }

            crossCutFrames.Add(new CrossCutFrameIssue(
                frameGroup.Key,
                frameGroup
                    .Select(file => $"C{file.CutNumber!.Value.Cut:D3}:{Path.GetFileName(file.Path.Value)}")
                    .ToList(),
                $"帧 {frameGroup.Key:D3} 出现在多个镜头：{string.Join(", ", cuts.Select(cut => $"C{cut.Cut:D3}"))}。"));
        }

        return new FrameSequenceAnalysisResult(
            true,
            parsed.Count,
            missingFrames,
            duplicateFrames,
            crossCutFrames,
            orphanFrames);
    }
}
