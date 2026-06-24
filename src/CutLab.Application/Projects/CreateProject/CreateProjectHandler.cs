namespace CutLab.Application.Projects.CreateProject;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed record CreateProjectCommand(
    string Name,
    int Episode,
    string NamingTemplate,
    string ArchivePathPattern,
    IReadOnlyList<string> ArchiveFolders,
    string RootPath);

public sealed class CreateProjectHandler
{
    private readonly IAnimationProjectRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProjectHandler(IAnimationProjectRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProjectId>> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        var naming = NamingConvention.Create(
            command.NamingTemplate,
            "_",
            DefaultTypeSuffixes());

        if (naming.IsFailure || naming.Value is null)
        {
            return Result.Failure<ProjectId>(naming.Error ?? "命名规则无效。");
        }

        var archive = ArchiveTemplate.Create(command.ArchivePathPattern, command.ArchiveFolders);
        if (archive.IsFailure || archive.Value is null)
        {
            return Result.Failure<ProjectId>(archive.Error ?? "归档模板无效。");
        }

        var project = AnimationProject.Create(
            command.Name,
            new EpisodeNumber(command.Episode),
            naming.Value,
            archive.Value,
            new WorkspacePath(command.RootPath));

        if (project.IsFailure || project.Value is null)
        {
            return Result.Failure<ProjectId>(project.Error ?? "项目创建失败。");
        }

        await _repository.SaveAsync(project.Value, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return Result.Success(project.Value.Id);
    }

    private static IReadOnlyDictionary<AssetType, string> DefaultTypeSuffixes() =>
        new Dictionary<AssetType, string>
        {
            [AssetType.Storyboard] = "分镜",
            [AssetType.Keyframe] = "原画",
            [AssetType.Inbetween] = "动画",
            [AssetType.Background] = "背景",
            [AssetType.Render] = "渲染"
        };
}
