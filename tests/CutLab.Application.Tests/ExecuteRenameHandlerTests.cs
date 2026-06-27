using CutLab.Application.Common.Interfaces;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Operations.UndoLastOperation;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ExecuteRenameHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldRenameRecognizedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "1卡原画.png");
            await File.WriteAllTextAsync(sourceFile, "test");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var batchRepository = new InMemoryOperationBatchRepository();
            var fileSystemGateway = new LocalFileSystemGateway();
            var unitOfWork = new JsonUnitOfWork();

            var createHandler = new CreateProjectHandler(projectRepository, unitOfWork);
            var createResult = await createHandler.HandleAsync(new CreateProjectCommand(
                "Test",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}/",
                ["原画"],
                tempDir));

            var projectId = createResult.Value!;
            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                fileSystemGateway,
                new RegexRecognitionService(),
                new TemplateNamingService());

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, tempDir, false));
            var executeHandler = new ExecuteRenameHandler(
                projectRepository,
                sessionRepository,
                batchRepository,
                fileSystemGateway,
                unitOfWork);

            var renameResult = await executeHandler.HandleAsync(
                new ExecuteRenameCommand(projectId, scanResult.Value!.SessionId, DryRun: false));

            Assert.True(renameResult.IsSuccess);
            Assert.Equal(1, renameResult.Value!.RenamedCount);
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(Path.Combine(tempDir, "EP01_S01_C001_原画.png")));

            var undoHandler = new UndoLastOperationHandler(batchRepository, fileSystemGateway, unitOfWork);
            var undoResult = await undoHandler.HandleAsync(new UndoLastOperationCommand(projectId));

            Assert.True(undoResult.IsSuccess);
            Assert.True(File.Exists(sourceFile));
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
