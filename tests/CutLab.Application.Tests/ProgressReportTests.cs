using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Reporting.ExportProgressReport;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Infrastructure.Export;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ExportProgressReportHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldExportProgressRows()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "progress.xlsx");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001_原画.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C002_分镜.png"), "b");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var projectId = (await new CreateProjectHandler(projectRepository, new JsonUnitOfWork()).HandleAsync(
                new CreateProjectCommand(
                    "Progress",
                    1,
                    "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                    "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                    ["分镜", "原画"],
                    tempDir))).Value!;

            var scanResult = await new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                new LocalFileSystemGateway(),
                new RegexRecognitionService(),
                new TemplateNamingService()).HandleAsync(new ScanFolderCommand(projectId, tempDir, false));

            var handler = new ExportProgressReportHandler(
                sessionRepository,
                new MiniExcelProgressReportExportService());

            var result = await handler.HandleAsync(new ExportProgressReportCommand(
                projectId,
                scanResult.Value!.SessionId,
                outputPath));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.RowCount);
            Assert.True(File.Exists(outputPath));
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

public class MissingInsertSuffixTests
{
    [Fact]
    public async Task HandleAsync_ShouldDetectMissingInsertSuffixes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001c_原画.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001e_原画.png"), "b");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var registryRepository = new InMemoryCutRegistryRepository();
            var projectId = (await new CreateProjectHandler(projectRepository, new JsonUnitOfWork()).HandleAsync(
                new CreateProjectCommand(
                    "Insert Gap",
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

            var result = await new GetMissingCutsFromSessionHandler(
                sessionRepository,
                registryRepository,
                new JsonUnitOfWork()).HandleAsync(
                new GetMissingCutsFromSessionQuery(projectId, scanResult.Value!.SessionId));

            Assert.True(result.IsSuccess);
            Assert.Contains(result.Value!.MissingInsertSuffixes, item => item.MissingSuffix == "b");
            Assert.Contains(result.Value.MissingInsertSuffixes, item => item.MissingSuffix == "d");
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
