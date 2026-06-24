namespace CutLab.Domain.Projects;

using CutLab.Domain.Common;
using CutLab.Domain.ValueObjects;

public sealed class AnimationProject : AggregateRoot<ProjectId>
{
    private AnimationProject(
        ProjectId id,
        string name,
        EpisodeNumber episode,
        NamingConvention namingConvention,
        ArchiveTemplate archiveTemplate,
        WorkspacePath rootPath,
        IReadOnlyList<RecognitionPattern> recognitionPatterns,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        Id = id;
        Name = name;
        Episode = episode;
        NamingConvention = namingConvention;
        ArchiveTemplate = archiveTemplate;
        RootPath = rootPath;
        RecognitionPatterns = recognitionPatterns;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;
    }

    public string Name { get; private set; }

    public EpisodeNumber Episode { get; private set; }

    public NamingConvention NamingConvention { get; private set; }

    public ArchiveTemplate ArchiveTemplate { get; private set; }

    public WorkspacePath RootPath { get; private set; }

    public IReadOnlyList<RecognitionPattern> RecognitionPatterns { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<AnimationProject> Create(
        string name,
        EpisodeNumber episode,
        NamingConvention namingConvention,
        ArchiveTemplate archiveTemplate,
        WorkspacePath rootPath,
        IReadOnlyList<RecognitionPattern>? recognitionPatterns = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<AnimationProject>("项目名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(rootPath.Value))
        {
            return Result.Failure<AnimationProject>("工作根路径不能为空。");
        }

        return Result.Success(new AnimationProject(
            ProjectId.New(),
            name.Trim(),
            episode,
            namingConvention,
            archiveTemplate,
            rootPath,
            recognitionPatterns ?? []));
    }

    public static AnimationProject Restore(
        ProjectId id,
        string name,
        EpisodeNumber episode,
        NamingConvention namingConvention,
        ArchiveTemplate archiveTemplate,
        WorkspacePath rootPath,
        IReadOnlyList<RecognitionPattern> recognitionPatterns,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(
            id,
            name.Trim(),
            episode,
            namingConvention,
            archiveTemplate,
            rootPath,
            recognitionPatterns,
            createdAt,
            updatedAt);

    public Result UpdateNamingConvention(NamingConvention convention)
    {
        NamingConvention = convention;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result UpdateArchiveTemplate(ArchiveTemplate template)
    {
        ArchiveTemplate = template;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result SetRootPath(WorkspacePath rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath.Value))
        {
            return Result.Failure("工作根路径不能为空。");
        }

        RootPath = rootPath;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
