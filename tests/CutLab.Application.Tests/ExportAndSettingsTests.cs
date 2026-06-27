using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Application.Reporting.ExportCutList;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;
using CutLab.Infrastructure.Export;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;

namespace CutLab.Application.Tests;

public class ExportCutListHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldExportExcelRows()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "cut-list.xlsx");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "1卡原画.png"), "test");

            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var sessionRepository = new InMemoryScanSessionRepository();
            var exportService = new MiniExcelCutListExportService();

            var createHandler = new CreateProjectHandler(projectRepository, new JsonUnitOfWork());
            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Test",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir))).Value!;

            var scanHandler = new ScanFolderHandler(
                projectRepository,
                sessionRepository,
                new LocalFileSystemGateway(),
                new RegexRecognitionService(),
                new TemplateNamingService());

            var scanResult = await scanHandler.HandleAsync(new ScanFolderCommand(projectId, tempDir, false));
            var handler = new ExportCutListHandler(sessionRepository, exportService);

            var result = await handler.HandleAsync(new ExportCutListCommand(
                projectId,
                scanResult.Value!.SessionId,
                outputPath));

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value!.RowCount);
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

public class UpdateProjectSettingsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldPersistUpdatedTemplate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectRepository = new JsonProjectRepository(Path.Combine(tempDir, "store"));
            var unitOfWork = new JsonUnitOfWork();
            var createHandler = new CreateProjectHandler(projectRepository, unitOfWork);
            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Old Name",
                1,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["原画"],
                tempDir))).Value!;

            var updateHandler = new UpdateProjectSettingsHandler(projectRepository, unitOfWork);
            var updateResult = await updateHandler.HandleAsync(new UpdateProjectSettingsCommand(
                projectId,
                "New Name",
                2,
                "C{CUT:03}_{TYPE}",
                "C{CUT:03}/{TYPE}",
                "分镜, 原画",
                tempDir));

            Assert.True(updateResult.IsSuccess);

            var project = await projectRepository.GetByIdAsync(projectId);
            Assert.NotNull(project);
            Assert.Equal("New Name", project!.Name);
            Assert.Equal(2, project.Episode.Value);
            Assert.Equal("C{CUT:03}_{TYPE}", project.NamingConvention.Template);
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
