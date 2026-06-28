namespace CutLab.Application.Projects.ProjectConfig;

using CutLab.Application.Common;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Domain.Common;
using CutLab.Domain.Cuts;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed class ProjectConfigDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Name { get; set; } = string.Empty;

    public int Episode { get; set; } = 1;

    public string NamingTemplate { get; set; } = "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}";

    public string ArchivePathPattern { get; set; } = "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}";

    public List<string> ArchiveFolders { get; set; } = ["分镜", "原画", "动画", "背景", "渲染"];

    public List<string> RecognitionPatterns { get; set; } = [];

    public string? DefaultVersionTag { get; set; }

    public string? RootPath { get; set; }

    public static ProjectConfigDocument FromProject(AnimationProject project, bool includeRootPath) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Name = project.Name,
            Episode = project.Episode.Value,
            NamingTemplate = project.NamingConvention.Template,
            ArchivePathPattern = project.ArchiveTemplate.PathPattern,
            ArchiveFolders = project.ArchiveTemplate.FolderNames.ToList(),
            RecognitionPatterns = project.RecognitionPatterns.Select(pattern => pattern.Pattern).ToList(),
            DefaultVersionTag = project.DefaultVersionTag?.Value,
            RootPath = includeRootPath ? project.RootPath.Value : null
        };

    public Result Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            return Result.Failure($"不支持的项目配置版本：{SchemaVersion}。");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result.Failure("项目名称不能为空。");
        }

        var naming = NamingConvention.Create(NamingTemplate, "_", DefaultTypeSuffixes());
        if (naming.IsFailure)
        {
            return Result.Failure(naming.Error ?? "命名规则无效。");
        }

        var archive = ArchiveTemplate.Create(ArchivePathPattern, ArchiveFolders);
        if (archive.IsFailure)
        {
            return Result.Failure(archive.Error ?? "归档模板无效。");
        }

        if (!string.IsNullOrWhiteSpace(DefaultVersionTag) && VersionTagParser.TryParse(DefaultVersionTag) is null)
        {
            return Result.Failure("版本标签格式无效（支持 v1、draft、s 等）。");
        }

        return Result.Success();
    }

    public Result<CreateProjectCommand> ToCreateProjectCommand(string fallbackRootPath)
    {
        var validation = Validate();
        if (validation.IsFailure)
        {
            return Result.Failure<CreateProjectCommand>(validation.Error ?? "项目配置无效。");
        }

        var rootPath = string.IsNullOrWhiteSpace(RootPath) ? fallbackRootPath : RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Result.Failure<CreateProjectCommand>("缺少工作根路径，请在导入前选择文件夹或于配置中填写 rootPath。");
        }

        return Result.Success(new CreateProjectCommand(
            Name.Trim(),
            Episode,
            NamingTemplate,
            ArchivePathPattern,
            ArchiveFolders,
            rootPath.Trim(),
            RecognitionPatternParser.Format(
                RecognitionPatterns
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(pattern => new RecognitionPattern(pattern.Trim()))
                    .ToList())));
    }

    public Result<UpdateProjectSettingsCommand> ToUpdateProjectSettingsCommand(ProjectId projectId, string fallbackRootPath)
    {
        var validation = Validate();
        if (validation.IsFailure)
        {
            return Result.Failure<UpdateProjectSettingsCommand>(validation.Error ?? "项目配置无效。");
        }

        var rootPath = string.IsNullOrWhiteSpace(RootPath) ? fallbackRootPath : RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Result.Failure<UpdateProjectSettingsCommand>("缺少工作根路径。");
        }

        return Result.Success(new UpdateProjectSettingsCommand(
            projectId,
            Name.Trim(),
            Episode,
            NamingTemplate,
            ArchivePathPattern,
            string.Join(", ", ArchiveFolders),
            rootPath.Trim(),
            DefaultVersionTag ?? string.Empty,
            RecognitionPatternParser.Format(
                RecognitionPatterns
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(pattern => new RecognitionPattern(pattern.Trim()))
                    .ToList())));
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
