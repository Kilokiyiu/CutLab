namespace CutLab.Application.Projects.ListRecentProjects;

using CutLab.Domain.Projects;

public sealed record ListRecentProjectsQuery(int Count = 5);

public sealed record ProjectSummaryDto(
    ProjectId Id,
    string Name,
    string RootPath,
    int Episode);

public sealed class ListRecentProjectsHandler
{
    private readonly IAnimationProjectRepository _repository;

    public ListRecentProjectsHandler(IAnimationProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProjectSummaryDto>> HandleAsync(
        ListRecentProjectsQuery query,
        CancellationToken cancellationToken = default)
    {
        var projects = await _repository.ListRecentAsync(query.Count, cancellationToken);
        return projects
            .Select(project => new ProjectSummaryDto(
                project.Id,
                project.Name,
                project.RootPath.Value,
                project.Episode.Value))
            .ToList();
    }
}
