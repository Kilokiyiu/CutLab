namespace CutLab.Application.Projects.UpdateProjectSettings;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed record UpdateProjectSettingsCommand(
    ProjectId ProjectId,
    string Name,
    int Episode,
    string NamingTemplate,
    string ArchivePathPattern,
    string ArchiveFoldersText,
    string RootPath,
    string DefaultVersionTag,
    string RecognitionPatternsText);

public sealed class UpdateProjectSettingsHandler
{
    private readonly IAnimationProjectRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProjectSettingsHandler(IAnimationProjectRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        UpdateProjectSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure("项目不存在。");
        }

        var naming = NamingConvention.Create(
            command.NamingTemplate,
            "_",
            DefaultTypeSuffixes());
        if (naming.IsFailure || naming.Value is null)
        {
            return Result.Failure(naming.Error ?? "命名规则无效。");
        }

        var folders = ParseFolders(command.ArchiveFoldersText);
        var archive = ArchiveTemplate.Create(command.ArchivePathPattern, folders);
        if (archive.IsFailure || archive.Value is null)
        {
            return Result.Failure(archive.Error ?? "归档模板无效。");
        }

        var infoResult = project.UpdateInfo(command.Name, new EpisodeNumber(command.Episode));
        if (infoResult.IsFailure)
        {
            return infoResult;
        }

        project.UpdateNamingConvention(naming.Value);
        project.UpdateArchiveTemplate(archive.Value);

        var rootResult = project.SetRootPath(new WorkspacePath(command.RootPath));
        if (rootResult.IsFailure)
        {
            return rootResult;
        }

        var versionResult = project.UpdateDefaultVersionTag(command.DefaultVersionTag);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        project.UpdateRecognitionPatterns(RecognitionPatternParser.Parse(command.RecognitionPatternsText));

        await _repository.SaveAsync(project, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static IReadOnlyList<string> ParseFolders(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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
