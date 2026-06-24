using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.Naming;

namespace CutLab.Application.Tests;

public class TemplateNamingServiceTests
{
    [Fact]
    public void GenerateFileName_ShouldRenderTemplate()
    {
        var service = new TemplateNamingService();
        var convention = NamingConvention.Create(
            "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
            "_",
            new Dictionary<AssetType, string>
            {
                [AssetType.Keyframe] = "原画"
            }).Value!;

        var result = service.GenerateFileName(
            convention,
            new CutNumber(1, 2, 3),
            AssetType.Keyframe,
            ".png");

        Assert.True(result.IsSuccess);
        Assert.Equal("EP01_S02_C003_原画.png", result.Value!.Value);
    }
}
