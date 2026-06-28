namespace CutLab.Application.Projects.ProjectConfig;

using System.Text.Json;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record ImportProjectConfigCommand(
    string FilePath,
    ProjectId? TargetProjectId,
    string? FallbackRootPath);

public sealed record ImportProjectConfigResult(
    ProjectId ProjectId,
    string ProjectName,
    bool CreatedNew);

public sealed class ImportProjectConfigHandler
{
    private readonly IAnimationProjectRepository _repository;
    private readonly CreateProjectHandler _createProjectHandler;
    private readonly UpdateProjectSettingsHandler _updateProjectSettingsHandler;

    public ImportProjectConfigHandler(
        IAnimationProjectRepository repository,
        CreateProjectHandler createProjectHandler,
        UpdateProjectSettingsHandler updateProjectSettingsHandler)
    {
        _repository = repository;
        _createProjectHandler = createProjectHandler;
        _updateProjectSettingsHandler = updateProjectSettingsHandler;
    }

    public async Task<Result<ImportProjectConfigResult>> HandleAsync(
        ImportProjectConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.FilePath) || !File.Exists(command.FilePath))
        {
            return Result.Failure<ImportProjectConfigResult>("项目配置文件不存在。");
        }

        ProjectConfigDocument document;
        try
        {
            document = await ProjectConfigSerializer.ReadAsync(command.FilePath, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            return Result.Failure<ImportProjectConfigResult>($"无法读取项目配置：{ex.Message}");
        }

        if (command.TargetProjectId is { } targetProjectId)
        {
            var project = await _repository.GetByIdAsync(targetProjectId, cancellationToken);
            if (project is null)
            {
                return Result.Failure<ImportProjectConfigResult>("目标项目不存在。");
            }

            var fallbackRoot = command.FallbackRootPath ?? project.RootPath.Value;
            var updateCommand = document.ToUpdateProjectSettingsCommand(targetProjectId, fallbackRoot);
            if (updateCommand.IsFailure || updateCommand.Value is null)
            {
                return Result.Failure<ImportProjectConfigResult>(updateCommand.Error ?? "项目配置无效。");
            }

            var updateResult = await _updateProjectSettingsHandler.HandleAsync(updateCommand.Value, cancellationToken);
            if (updateResult.IsFailure)
            {
                return Result.Failure<ImportProjectConfigResult>(updateResult.Error ?? "导入项目配置失败。");
            }

            return Result.Success(new ImportProjectConfigResult(
                targetProjectId,
                document.Name.Trim(),
                CreatedNew: false));
        }

        var createCommand = document.ToCreateProjectCommand(command.FallbackRootPath ?? string.Empty);
        if (createCommand.IsFailure || createCommand.Value is null)
        {
            return Result.Failure<ImportProjectConfigResult>(createCommand.Error ?? "项目配置无效。");
        }

        var createResult = await _createProjectHandler.HandleAsync(createCommand.Value, cancellationToken);
        if (createResult.IsFailure)
        {
            return Result.Failure<ImportProjectConfigResult>(createResult.Error ?? "创建项目失败。");
        }

        return Result.Success(new ImportProjectConfigResult(
            createResult.Value,
            document.Name.Trim(),
            CreatedNew: true));
    }
}
