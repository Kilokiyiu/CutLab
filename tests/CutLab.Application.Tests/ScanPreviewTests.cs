using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Scanning.GetScanPreview;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ScanPreviewTests
{
    [Fact]
    public async Task GetScanPreview_ShouldListAlreadyNamedInsertCuts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001c_原画.png"), "x");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001e_原画.png"), "x");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var projectId = (await new CreateProjectHandler(projectRepository, new JsonUnitOfWork()).HandleAsync(
                new CreateProjectCommand(
                    "T",
                    1,
                    "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                    "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                    ["原画"],
                    tempDir))).Value!;

            var scanResult = await new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                new LocalFileSystemGateway(),
                new RegexRecognitionService(),
                new TemplateNamingService()).HandleAsync(new ScanFolderCommand(projectId, tempDir, false));

            var preview = await new GetScanPreviewHandler(sessionRepository).HandleAsync(
                new GetScanPreviewQuery(scanResult.Value!.SessionId));

            Assert.Equal(2, scanResult.Value!.RecognizedCount);
            Assert.Equal(2, preview.Value!.Items.Count);
            Assert.Equal(2, preview.Value.AlreadyNamedCount);
            Assert.All(preview.Value.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.CutId)));
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
    public async Task GetScanPreview_WithVersionFilter_ShouldHideUntaggedAssets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001c_原画.png"), "x");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var projectId = (await new CreateProjectHandler(projectRepository, new JsonUnitOfWork()).HandleAsync(
                new CreateProjectCommand(
                    "T",
                    1,
                    "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                    "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                    ["原画"],
                    tempDir))).Value!;

            var scanResult = await new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                new LocalFileSystemGateway(),
                new RegexRecognitionService(),
                new TemplateNamingService()).HandleAsync(new ScanFolderCommand(projectId, tempDir, false));

            var preview = await new GetScanPreviewHandler(sessionRepository).HandleAsync(
                new GetScanPreviewQuery(scanResult.Value!.SessionId, "v1"));

            Assert.Equal(1, scanResult.Value!.RecognizedCount);
            Assert.Empty(preview.Value!.Items);
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
