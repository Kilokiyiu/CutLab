using CutLab.Application.Common;
using CutLab.Domain.ValueObjects;

namespace CutLab.Application.Tests;

public class TargetPathConflictResolverTests
{
    [Fact]
    public void ResolveRename_AutoSuffix_ShouldAllocateUniquePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var existing = Path.Combine(tempDir, "EP01_S01_C001_原画.png");
            File.WriteAllText(existing, "exists");

            var reserved = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var resolution = TargetPathConflictResolver.ResolveRename(
                existing,
                new FileName("EP01_S01_C001_原画.png"),
                Guid.NewGuid(),
                reserved,
                ConflictResolutionStrategy.AutoSuffix);

            Assert.Equal(RenamePlanStatus.Ready, resolution.Status);
            Assert.NotEqual(existing, resolution.TargetPath.Value, StringComparer.OrdinalIgnoreCase);
            Assert.EndsWith("_1.png", resolution.TargetPath.Value);
            Assert.True(reserved.ContainsKey(resolution.TargetPath.Value));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveRename_Skip_ShouldMarkSkippedWhenTargetExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var existing = Path.Combine(tempDir, "target.png");
            File.WriteAllText(existing, "exists");

            var reserved = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var resolution = TargetPathConflictResolver.ResolveRename(
                existing,
                new FileName("target.png"),
                Guid.NewGuid(),
                reserved,
                ConflictResolutionStrategy.Skip);

            Assert.Equal(RenamePlanStatus.Skipped, resolution.Status);
            Assert.Contains("已跳过", resolution.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
