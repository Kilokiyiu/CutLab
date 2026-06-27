namespace CutLab.Application.Projects.GetProject;

using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record GetProjectQuery(ProjectId ProjectId);

public sealed record ProjectSettingsDto(
    ProjectId Id,
    string Name,
    int Episode,
    string NamingTemplate,
    string ArchivePathPattern,
    string ArchiveFoldersText,
    string RootPath);

public sealed class GetProjectHandler
{
    private readonly IAnimationProjectRepository _repository;

    public GetProjectHandler(IAnimationProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ProjectSettingsDto>> HandleAsync(
        GetProjectQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(query.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ProjectSettingsDto>("项目不存在。");
        }

        return Result.Success(new ProjectSettingsDto(
            project.Id,
            project.Name,
            project.Episode.Value,
            project.NamingConvention.Template,
            project.ArchiveTemplate.PathPattern,
            string.Join(", ", project.ArchiveTemplate.FolderNames),
            project.RootPath.Value));
    }
}
