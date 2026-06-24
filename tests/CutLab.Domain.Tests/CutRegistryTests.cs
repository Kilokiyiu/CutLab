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
}
