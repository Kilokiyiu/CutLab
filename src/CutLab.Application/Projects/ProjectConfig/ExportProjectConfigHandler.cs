namespace CutLab.Application.Projects.ProjectConfig;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record ExportProjectConfigCommand(
    ProjectId ProjectId,
    string OutputPath,
    bool IncludeRootPath = true);

public sealed record ExportProjectConfigResult(string OutputPath, string ProjectName);

public sealed class ExportProjectConfigHandler
{
    private readonly IAnimationProjectRepository _repository;

    public ExportProjectConfigHandler(IAnimationProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ExportProjectConfigResult>> HandleAsync(
        ExportProjectConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<ExportProjectConfigResult>("导出路径不能为空。");
        }

        var project = await _repository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ExportProjectConfigResult>("项目不存在。");
        }

        var document = ProjectConfigDocument.FromProject(project, command.IncludeRootPath);
        await ProjectConfigSerializer.WriteAsync(command.OutputPath, document, cancellationToken);
        return Result.Success(new ExportProjectConfigResult(command.OutputPath, project.Name));
    }
}
