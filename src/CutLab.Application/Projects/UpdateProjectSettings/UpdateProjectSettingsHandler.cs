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
    string NamingSeparator,
    string ArchivePathPattern,
    string ArchiveFoldersText,
    string RootPath,
    string DefaultVersionTag,
    string RecognitionPatternsText,
    string TypeSuffixesText,
    bool FrameSequenceEnabled,
    string FrameSequencePattern,
    int FrameSequenceMinFrame,
    int FrameSequenceMaxFrame);

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
            string.IsNullOrWhiteSpace(command.NamingSeparator) ? "_" : command.NamingSeparator,
            TypeSuffixesParser.Parse(command.TypeSuffixesText));
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

        var frameSettings = FrameSequenceSettings.Create(
            command.FrameSequenceEnabled,
            command.FrameSequencePattern,
            command.FrameSequenceMinFrame,
            command.FrameSequenceMaxFrame);
        if (frameSettings.IsFailure || frameSettings.Value is null)
        {
            return Result.Failure(frameSettings.Error ?? "帧序列设置无效。");
        }

        project.UpdateFrameSequenceSettings(frameSettings.Value);

        await _repository.SaveAsync(project, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static IReadOnlyList<string> ParseFolders(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
