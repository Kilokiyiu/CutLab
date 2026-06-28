using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.DeleteProject;
using CutLab.Application.Reporting.ExportMissingCuts;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ExportMissingCutsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldExportMissingCutsAsCsv()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001_原画.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C003_原画.png"), "c");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var registryRepository = new InMemoryCutRegistryRepository();
            var unitOfWork = new JsonUnitOfWork();
            var createHandler = new CreateProjectHandler(projectRepository, unitOfWork);
            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                new LocalFileSystemGateway(),
                new RegexRecognitionService(),
                new TemplateNamingService());

            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Missing Export",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir))).Value!;

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, tempDir, false));
            var getMissingHandler = new GetMissingCutsFromSessionHandler(
                sessionRepository,
                registryRepository,
                unitOfWork);
            var exportHandler = new ExportMissingCutsHandler(getMissingHandler);

            var outputPath = Path.Combine(tempDir, "missing.csv");
            var result = await exportHandler.HandleAsync(new ExportMissingCutsCommand(
                projectId,
                scanResult.Value!.SessionId,
                outputPath,
                ScopeFromCut: 1,
                ScopeToCut: 3));

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(outputPath));
            var content = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("C002", content);
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

public class DeleteProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldRemoveProjectFromStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var unitOfWork = new JsonUnitOfWork();
            var createHandler = new CreateProjectHandler(repository, unitOfWork);
            var deleteHandler = new DeleteProjectHandler(repository, unitOfWork);

            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Delete Me",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir))).Value!;

            var deleteResult = await deleteHandler.HandleAsync(new DeleteProjectCommand(projectId));
            Assert.True(deleteResult.IsSuccess);
            Assert.Null(await repository.GetByIdAsync(projectId));
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
