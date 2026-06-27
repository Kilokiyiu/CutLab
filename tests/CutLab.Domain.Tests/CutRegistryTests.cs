using CutLab.Domain.Projects;
using CutLab.Domain.Cuts;
using CutLab.Domain.ValueObjects;

namespace CutLab.Domain.Tests;

public class CutRegistryTests
{
    [Fact]
    public void DetectGaps_ShouldReturnMissingCuts()
    {
        var registry = CutRegistry.Create(
            ProjectId.New(),
            new CutScope(
                new EpisodeNumber(1),
                2,
                new CutNumber(1, 2, 1),
                new CutNumber(1, 2, 5)));

        registry.RegisterCut(new CutNumber(1, 2, 1));
        registry.RegisterCut(new CutNumber(1, 2, 3));
        registry.RegisterCut(new CutNumber(1, 2, 5));

        var missing = registry.DetectGaps();

        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, m => m.CutNumber.Cut == 2);
        Assert.Contains(missing, m => m.CutNumber.Cut == 4);
    }

    [Fact]
    public void CreateInsertCut_ShouldAssignNextSuffix()
    {
        var registry = CutRegistry.Create(
            ProjectId.New(),
            new CutScope(
                new EpisodeNumber(1),
                1,
                new CutNumber(1, 1, 1),
                new CutNumber(1, 1, 5)));

        registry.RegisterCut(new CutNumber(1, 1, 3));
        registry.RegisterCut(new CutNumber(1, 1, 3, "b"));

        var result = registry.CreateInsertCut(3, 1, 1, ["b"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("c", result.Value!.InsertSuffix);
    }

    [Fact]
    public void DetectInsertSuffixGaps_ShouldReturnMissingLetters()
    {
        var registry = CutRegistry.Create(
            ProjectId.New(),
            new CutScope(
                new EpisodeNumber(1),
                1,
                new CutNumber(1, 1, 1),
                new CutNumber(1, 1, 5)));

        registry.RegisterCut(new CutNumber(1, 1, 1, "c"));
        registry.RegisterCut(new CutNumber(1, 1, 1, "e"));
        registry.RegisterCut(new CutNumber(1, 1, 1, "g"));
        registry.RegisterCut(new CutNumber(1, 1, 1, "i"));

        var missing = registry.DetectInsertSuffixGaps();

        Assert.Equal(4, missing.Count);
        Assert.Contains(missing, item => item.MissingSuffix == "b");
        Assert.Contains(missing, item => item.MissingSuffix == "d");
        Assert.Contains(missing, item => item.MissingSuffix == "f");
        Assert.Contains(missing, item => item.MissingSuffix == "h");
    }
}
