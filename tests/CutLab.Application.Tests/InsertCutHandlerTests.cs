using CutLab.Application.Operations.InsertCut;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class InsertCutHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldRenameUnrecognizedFilesToInsertCut()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S02_C001_原画.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S02_C003_原画.png"), "b");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "new-cut.png"), "c");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var registryRepository = new InMemoryCutRegistryRepository();
            var batchRepository = new InMemoryOperationBatchRepository();
            var fileSystem = new LocalFileSystemGateway();
            var naming = new TemplateNamingService();
            var unitOfWork = new JsonUnitOfWork();

            var createHandler = new CreateProjectHandler(projectRepository, unitOfWork);
            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Insert Test",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir))).Value!;

            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                fileSystem,
                new RegexRecognitionService(),
                naming);

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, tempDir, false));
            var handler = new InsertCutHandler(
                projectRepository,
                sessionRepository,
                registryRepository,
                batchRepository,
                fileSystem,
                naming,
                unitOfWork);

            var result = await handler.HandleAsync(new InsertCutCommand(
                projectId,
                scanResult.Value!.SessionId,
                AfterCut: 3,
                AssetType.Keyframe,
                UnrecognizedOnly: true,
                DryRun: false));

            Assert.True(result.IsSuccess);
            Assert.Equal("b", result.Value!.InsertCutNumber.InsertSuffix);
            Assert.Equal(1, result.Value.RenamedCount);
            Assert.True(File.Exists(Path.Combine(tempDir, "EP01_S02_C003b_原画.png")));
            Assert.False(File.Exists(Path.Combine(tempDir, "new-cut.png")));
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
