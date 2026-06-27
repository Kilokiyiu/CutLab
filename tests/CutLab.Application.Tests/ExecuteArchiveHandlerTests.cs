using CutLab.Application.Common;
using CutLab.Application.Operations.ExecuteArchive;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;
using CutLab.Infrastructure.Archive;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ExecuteArchiveHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateArchiveDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tempDir, "raw");
        Directory.CreateDirectory(sourceDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "1卡原画.png"), "test");

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
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["分镜", "原画", "动画", "背景", "渲染"],
                tempDir));

            var projectId = createResult.Value!;
            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                fileSystemGateway,
                new RegexRecognitionService(),
                new TemplateNamingService());

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, sourceDir, false));
            var archiveHandler = new ExecuteArchiveHandler(
                projectRepository,
                sessionRepository,
                batchRepository,
                fileSystemGateway,
                new TemplateArchivePathResolver(),
                unitOfWork);

            var result = await archiveHandler.HandleAsync(new ExecuteArchiveCommand(
                projectId,
                scanResult.Value!.SessionId,
                ArchiveExecutionMode.CreateDirectoriesOnly,
                DryRun: false));

            Assert.True(result.IsSuccess);
            Assert.True(Directory.Exists(Path.Combine(tempDir, "EP01", "S01", "C001", "原画")));
            Assert.True(Directory.Exists(Path.Combine(tempDir, "EP01", "S01", "C001", "分镜")));
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
    public async Task HandleAsync_ShouldMoveFilesIntoArchive()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tempDir, "raw");
        Directory.CreateDirectory(sourceDir);

        try
        {
            var sourceFile = Path.Combine(sourceDir, "1卡原画.png");
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
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir));

            var projectId = createResult.Value!;
            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                fileSystemGateway,
                new RegexRecognitionService(),
                new TemplateNamingService());

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, sourceDir, false));

            var renameHandler = new ExecuteRenameHandler(
                projectRepository,
                sessionRepository,
                batchRepository,
                fileSystemGateway,
                unitOfWork);
            await renameHandler.HandleAsync(new ExecuteRenameCommand(
                projectId,
                scanResult.Value!.SessionId,
                DryRun: false));

            var rescan = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, sourceDir, false));
            var archiveHandler = new ExecuteArchiveHandler(
                projectRepository,
                sessionRepository,
                batchRepository,
                fileSystemGateway,
                new TemplateArchivePathResolver(),
                unitOfWork);

            var result = await archiveHandler.HandleAsync(new ExecuteArchiveCommand(
                projectId,
                rescan.Value!.SessionId,
                ArchiveExecutionMode.MoveFiles,
                DryRun: false));

            Assert.True(result.IsSuccess);
            var targetFile = Path.Combine(tempDir, "EP01", "S01", "C001", "原画", "EP01_S01_C001_原画.png");
            Assert.True(File.Exists(targetFile));
            Assert.False(File.Exists(sourceFile));
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
