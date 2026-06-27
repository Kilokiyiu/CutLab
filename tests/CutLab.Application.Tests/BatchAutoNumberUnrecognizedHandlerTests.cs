using CutLab.Application.Operations.BatchAutoNumberUnrecognized;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class BatchAutoNumberUnrecognizedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldAssignSequentialCutNumbersToUnrecognizedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "b-photo.png"), "b");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "a-photo.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "EP01_S01_C001_原画.png"), "existing");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var batchRepository = new InMemoryOperationBatchRepository();
            var fileSystem = new LocalFileSystemGateway();
            var naming = new TemplateNamingService();
            var unitOfWork = new JsonUnitOfWork();

            var createHandler = new CreateProjectHandler(projectRepository, unitOfWork);
            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Auto Number Test",
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
            var handler = new BatchAutoNumberUnrecognizedHandler(
                projectRepository,
                sessionRepository,
                batchRepository,
                fileSystem,
                naming,
                unitOfWork);

            var preview = await handler.HandleAsync(new BatchAutoNumberUnrecognizedCommand(
                projectId,
                scanResult.Value!.SessionId,
                StartCutNumber: 1,
                DefaultAssetType: AssetType.Keyframe,
                DryRun: true));

            Assert.True(preview.IsSuccess);
            Assert.Equal(2, preview.Value!.ReadyCount);
            Assert.Contains(
                preview.Value.Items,
                item => item.ProposedFileName.Value == "EP01_S01_C002_原画.png"
                        && item.SourcePath.Value.EndsWith("a-photo.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                preview.Value.Items,
                item => item.ProposedFileName.Value == "EP01_S01_C003_原画.png"
                        && item.SourcePath.Value.EndsWith("b-photo.png", StringComparison.OrdinalIgnoreCase));

            var result = await handler.HandleAsync(new BatchAutoNumberUnrecognizedCommand(
                projectId,
                scanResult.Value!.SessionId,
                StartCutNumber: 1,
                DefaultAssetType: AssetType.Keyframe,
                DryRun: false));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.RenamedCount);
            Assert.True(File.Exists(Path.Combine(tempDir, "EP01_S01_C002_原画.png")));
            Assert.True(File.Exists(Path.Combine(tempDir, "EP01_S01_C003_原画.png")));
            Assert.False(File.Exists(Path.Combine(tempDir, "a-photo.png")));
            Assert.False(File.Exists(Path.Combine(tempDir, "b-photo.png")));
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
