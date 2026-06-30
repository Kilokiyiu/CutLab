namespace CutLab.Application.Projects.CreateProject;

using CutLab.Application.Common;
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
    string RootPath,
    string RecognitionPatternsText = "",
    string TypeSuffixesText = "",
    string NamingSeparator = "_",
    bool FrameSequenceEnabled = false,
    string FrameSequencePattern = "C{CUT:03}_{FRAME:03}",
    int FrameSequenceMinFrame = 1,
    int FrameSequenceMaxFrame = 99);

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
            string.IsNullOrWhiteSpace(command.NamingSeparator) ? "_" : command.NamingSeparator,
            TypeSuffixesParser.Parse(command.TypeSuffixesText));

        if (naming.IsFailure || naming.Value is null)
        {
            return Result.Failure<ProjectId>(naming.Error ?? "命名规则无效。");
        }

        var archive = ArchiveTemplate.Create(command.ArchivePathPattern, command.ArchiveFolders);
        if (archive.IsFailure || archive.Value is null)
        {
            return Result.Failure<ProjectId>(archive.Error ?? "归档模板无效。");
        }

        var frameSettings = FrameSequenceSettings.Create(
            command.FrameSequenceEnabled,
            command.FrameSequencePattern,
            command.FrameSequenceMinFrame,
            command.FrameSequenceMaxFrame);
        if (frameSettings.IsFailure || frameSettings.Value is null)
        {
            return Result.Failure<ProjectId>(frameSettings.Error ?? "帧序列设置无效。");
        }

        var project = AnimationProject.Create(
            command.Name,
            new EpisodeNumber(command.Episode),
            naming.Value,
            archive.Value,
            new WorkspacePath(command.RootPath),
            RecognitionPatternParser.Parse(command.RecognitionPatternsText),
            frameSettings.Value);

        if (project.IsFailure || project.Value is null)
        {
            return Result.Failure<ProjectId>(project.Error ?? "项目创建失败。");
        }

        await _repository.SaveAsync(project.Value, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return Result.Success(project.Value.Id);
    }
}
