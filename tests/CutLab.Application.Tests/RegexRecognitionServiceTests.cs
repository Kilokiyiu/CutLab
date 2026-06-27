using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class RegexRecognitionServiceTests
{
    private readonly RegexRecognitionService _service = new();
    private readonly NamingConvention _convention = NamingConvention.Create(
        "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
        "_",
        new Dictionary<AssetType, string> { [AssetType.Keyframe] = "原画" }).Value!;

    [Theory]
    [InlineData("1卡原画.png", 1, 1, 1, AssetType.Keyframe)]
    [InlineData("C003_分镜.jpg", 1, 1, 3, AssetType.Storyboard)]
    [InlineData("EP02_S05_C010_动画.psd", 2, 5, 10, AssetType.Inbetween)]
    public void TryParse_ShouldRecognizeCommonPatterns(
        string fileName,
        int episode,
        int scene,
        int cut,
        AssetType assetType)
    {
        var result = _service.TryParse(fileName, [], _convention);

        Assert.Equal(RecognitionStatus.Recognized, result.Status);
        Assert.NotNull(result.CutNumber);
        Assert.Equal(episode, result.CutNumber.Value.Episode);
        Assert.Equal(scene, result.CutNumber.Value.Scene);
        Assert.Equal(cut, result.CutNumber.Value.Cut);
        Assert.Equal(assetType, result.AssetType);
    }

    [Fact]
    public void TryParse_ShouldMarkOrdinalCardWithoutTypeAsUnrecognized()
    {
        var result = _service.TryParse("第3卡.png", [], _convention);

        Assert.Equal(RecognitionStatus.Unrecognized, result.Status);
        Assert.NotNull(result.CutNumber);
        Assert.Null(result.AssetType);
    }
}
