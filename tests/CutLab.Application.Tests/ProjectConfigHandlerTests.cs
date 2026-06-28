using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.ProjectConfig;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Infrastructure.Persistence;

namespace CutLab.Application.Tests;

public class ProjectConfigHandlerTests
{
    [Fact]
    public async Task ExportAndImport_ShouldRoundTripProjectSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var storeDir = Path.Combine(tempDir, "store");
            var repository = new JsonProjectRepository(storeDir);
            var unitOfWork = new JsonUnitOfWork();
            var createHandler = new CreateProjectHandler(repository, unitOfWork);
            var updateHandler = new UpdateProjectSettingsHandler(repository, unitOfWork);
            var exportHandler = new ExportProjectConfigHandler(repository);
            var importHandler = new ImportProjectConfigHandler(repository, createHandler, updateHandler);

            var projectId = (await createHandler.HandleAsync(new CreateProjectCommand(
                "Export Test",
                2,
                "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ["分镜", "原画"],
                tempDir,
                "{N}卡{TYPE}"))).Value!;

            var configPath = Path.Combine(tempDir, "project.cutlab.json");
            var exportResult = await exportHandler.HandleAsync(new ExportProjectConfigCommand(
                projectId,
                configPath,
                IncludeRootPath: true));

            Assert.True(exportResult.IsSuccess);
            Assert.True(File.Exists(configPath));

            var importResult = await importHandler.HandleAsync(new ImportProjectConfigCommand(
                configPath,
                TargetProjectId: projectId,
                FallbackRootPath: tempDir));

            Assert.True(importResult.IsSuccess);
            Assert.False(importResult.Value!.CreatedNew);
            Assert.Equal("Export Test", importResult.Value.ProjectName);

            var project = await repository.GetByIdAsync(projectId);
            Assert.NotNull(project);
            Assert.Equal(2, project!.Episode.Value);
            Assert.Equal(["分镜", "原画"], project.ArchiveTemplate.FolderNames);
            Assert.Equal(["{N}卡{TYPE}"], project.RecognitionPatterns.Select(pattern => pattern.Pattern).ToList());
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
    public async Task Import_ShouldCreateNewProjectWhenNoTargetSpecified()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CutLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var storeDir = Path.Combine(tempDir, "store");
            var repository = new JsonProjectRepository(storeDir);
            var unitOfWork = new JsonUnitOfWork();
            var createHandler = new CreateProjectHandler(repository, unitOfWork);
            var updateHandler = new UpdateProjectSettingsHandler(repository, unitOfWork);
            var importHandler = new ImportProjectConfigHandler(repository, createHandler, updateHandler);

            var configPath = Path.Combine(tempDir, "template.cutlab.json");
            await ProjectConfigSerializer.WriteAsync(configPath, new ProjectConfigDocument
            {
                Name = "Imported Template",
                Episode = 3,
                NamingTemplate = "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                ArchivePathPattern = "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ArchiveFolders = ["原画", "动画"],
                RootPath = tempDir
            });

            var importResult = await importHandler.HandleAsync(new ImportProjectConfigCommand(
                configPath,
                TargetProjectId: null,
                FallbackRootPath: tempDir));

            Assert.True(importResult.IsSuccess);
            Assert.True(importResult.Value!.CreatedNew);

            var project = await repository.GetByIdAsync(importResult.Value.ProjectId);
            Assert.NotNull(project);
            Assert.Equal("Imported Template", project!.Name);
            Assert.Equal(3, project.Episode.Value);
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
