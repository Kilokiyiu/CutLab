using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class GetMissingCutsFromSessionHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldDetectMissingCutsInRange()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "1卡原画.png"), "a");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "3卡原画.png"), "b");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var registryRepository = new InMemoryCutRegistryRepository();
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

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, tempDir, false));
            var handler = new GetMissingCutsFromSessionHandler(
                sessionRepository,
                registryRepository,
                unitOfWork);

            var result = await handler.HandleAsync(
                new GetMissingCutsFromSessionQuery(projectId, scanResult.Value!.SessionId));

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value!.MissingCuts);
            Assert.Equal(2, result.Value.MissingCuts[0].CutNumber.Cut);
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
