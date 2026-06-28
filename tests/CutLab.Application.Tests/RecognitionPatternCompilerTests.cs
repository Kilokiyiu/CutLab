using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class RecognitionPatternCompilerTests
{
    [Theory]
    [InlineData("{N}卡{TYPE}", "12卡原画")]
    [InlineData("EP{EP}_S{SC}_C{CUT}_{TYPE}", "EP02_S05_C010_动画")]
    [InlineData("C{CUT:03}_{TYPE}", "C003_分镜")]
    [InlineData("第{N}卡", "第3卡")]
    public void TryCompile_ShouldMatchTemplatePatterns(string pattern, string sample)
    {
        var regex = RecognitionPatternCompiler.TryCompile(pattern);

        Assert.NotNull(regex);
        Assert.True(regex!.IsMatch(sample));
    }

    [Fact]
    public void TryCompile_ShouldAcceptRawRegex()
    {
        var regex = RecognitionPatternCompiler.TryCompile(@"^C(?<cut>\d+)_(?<type>原画)$");

        Assert.NotNull(regex);
        Assert.True(regex!.IsMatch("C005_原画"));
    }
}

public class CustomRecognitionPatternTests
{
    private readonly RegexRecognitionService _service = new();
    private readonly NamingConvention _convention = NamingConvention.Create(
        "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
        "_",
        new Dictionary<AssetType, string> { [AssetType.Keyframe] = "原画" }).Value!;

    [Fact]
    public void TryParse_ShouldUseProjectRecognitionPatterns()
    {
        var patterns = new List<RecognitionPattern>
        {
            new("SHOT_{CUT}_{TYPE}")
        };

        var result = _service.TryParse("SHOT_007_背景.png", patterns, _convention);

        Assert.Equal(RecognitionStatus.Recognized, result.Status);
        Assert.Equal(7, result.CutNumber!.Value.Cut);
        Assert.Equal(AssetType.Background, result.AssetType);
    }

    [Fact]
    public void TryParse_ShouldUseCustomPatternWhenBuiltinDoesNotMatch()
    {
        var patterns = new List<RecognitionPattern>
        {
            new("镜头{N}_{TYPE}")
        };

        var result = _service.TryParse("镜头12_渲染.png", patterns, _convention);

        Assert.Equal(RecognitionStatus.Recognized, result.Status);
        Assert.Equal(12, result.CutNumber!.Value.Cut);
        Assert.Equal(AssetType.Render, result.AssetType);
    }
}
