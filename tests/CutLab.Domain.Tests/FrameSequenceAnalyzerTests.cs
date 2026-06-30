using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

namespace CutLab.Domain.Tests;

public class FrameSequenceAnalyzerTests
{
    private readonly FrameSequenceAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_ShouldDetectDuplicateAndCrossCutFrames()
    {
        var settings = FrameSequenceSettings.Create(
            true,
            "C{CUT:03}_{FRAME:03}",
            1,
            99).Value!;

        var files = new List<ParsedFrameFile>
        {
            new(new FilePath(@"D:\C001_001.png"), new CutNumber(1, 1, 1), 1),
            new(new FilePath(@"D:\C001_001_copy.png"), new CutNumber(1, 1, 1), 1),
            new(new FilePath(@"D:\C002_001.png"), new CutNumber(1, 1, 2), 1),
            new(new FilePath(@"D:\C001_003.png"), new CutNumber(1, 1, 1), 3)
        };

        var result = _analyzer.Analyze(settings, files);

        Assert.Single(result.DuplicateFrames);
        Assert.Single(result.CrossCutFrames);
        Assert.Single(result.MissingFrames);
        Assert.Equal(2, result.MissingFrames[0].FrameNumber);
    }
}
