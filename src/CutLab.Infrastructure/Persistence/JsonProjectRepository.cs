namespace CutLab.Infrastructure.Persistence;

using System.Text.Json;
using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Cuts;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

internal sealed class JsonProjectRepository : IAnimationProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _storeDirectory;

    public JsonProjectRepository(string storeDirectory)
    {
        _storeDirectory = storeDirectory;
        Directory.CreateDirectory(_storeDirectory);
    }

    public async Task<AnimationProject?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<AnimationProjectDto>(stream, JsonOptions, cancellationToken);
        return dto?.ToDomain();
    }

    public async Task SaveAsync(AnimationProject project, CancellationToken cancellationToken = default)
    {
        var path = GetPath(project.Id);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, AnimationProjectDto.FromDomain(project), JsonOptions, cancellationToken);
    }

    public Task DeleteAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetPath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AnimationProject>> ListRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_storeDirectory))
        {
            return [];
        }

        var files = Directory.GetFiles(_storeDirectory, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(count);

        var projects = new List<AnimationProject>();
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var dto = await JsonSerializer.DeserializeAsync<AnimationProjectDto>(stream, JsonOptions, cancellationToken);
            if (dto?.ToDomain() is { } project)
            {
                projects.Add(project);
            }
        }

        return projects;
    }

    private string GetPath(ProjectId id) => Path.Combine(_storeDirectory, $"{id.Value}.json");
}

internal sealed class AnimationProjectDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Episode { get; set; }

    public string NamingTemplate { get; set; } = "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}";

    public string NamingSeparator { get; set; } = "_";

    public string ArchivePathPattern { get; set; } = "{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}/";

    public List<string> ArchiveFolders { get; set; } = ["分镜", "原画", "动画", "背景", "渲染"];

    public List<string> RecognitionPatterns { get; set; } = [];

    public List<string> TypeSuffixes { get; set; } = ["分镜", "原画", "动画", "背景", "渲染"];

    public string RootPath { get; set; } = string.Empty;

    public string? DefaultVersionTag { get; set; }

    public bool FrameSequenceEnabled { get; set; }

    public string FrameSequencePattern { get; set; } = "C{CUT:03}_{FRAME:03}";

    public int FrameSequenceMinFrame { get; set; } = 1;

    public int FrameSequenceMaxFrame { get; set; } = 99;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static AnimationProjectDto FromDomain(AnimationProject project) =>
        new()
        {
            Id = project.Id.Value,
            Name = project.Name,
            Episode = project.Episode.Value,
            NamingTemplate = project.NamingConvention.Template,
            NamingSeparator = project.NamingConvention.Separator,
            ArchivePathPattern = project.ArchiveTemplate.PathPattern,
            ArchiveFolders = project.ArchiveTemplate.FolderNames.ToList(),
            RecognitionPatterns = project.RecognitionPatterns.Select(pattern => pattern.Pattern).ToList(),
            TypeSuffixes = TypeSuffixesParser.ToOrderedList(project.NamingConvention.TypeSuffixes),
            RootPath = project.RootPath.Value,
            DefaultVersionTag = project.DefaultVersionTag?.Value,
            FrameSequenceEnabled = project.FrameSequenceSettings.Enabled,
            FrameSequencePattern = project.FrameSequenceSettings.FileNamePattern,
            FrameSequenceMinFrame = project.FrameSequenceSettings.MinFrame,
            FrameSequenceMaxFrame = project.FrameSequenceSettings.MaxFrame,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };

    public AnimationProject? ToDomain()
    {
        var suffixes = TypeSuffixes.Count > 0
            ? TypeSuffixesParser.Parse(string.Join(", ", TypeSuffixes))
            : TypeSuffixesParser.Default();

        var naming = NamingConvention.Create(
            NamingTemplate,
            string.IsNullOrWhiteSpace(NamingSeparator) ? "_" : NamingSeparator,
            suffixes);

        var archive = ArchiveTemplate.Create(ArchivePathPattern, ArchiveFolders);
        if (naming.IsFailure || archive.IsFailure || naming.Value is null || archive.Value is null)
        {
            return null;
        }

        var frameSettings = FrameSequenceSettings.Create(
            FrameSequenceEnabled,
            FrameSequencePattern,
            FrameSequenceMinFrame,
            FrameSequenceMaxFrame);
        if (frameSettings.IsFailure || frameSettings.Value is null)
        {
            return null;
        }

        return AnimationProject.Restore(
            new ProjectId(Id),
            Name,
            new EpisodeNumber(Episode),
            naming.Value,
            archive.Value,
            new WorkspacePath(RootPath),
            RecognitionPatterns
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(pattern => new RecognitionPattern(pattern.Trim()))
                .ToList(),
            CreatedAt == default ? DateTimeOffset.UtcNow : CreatedAt,
            UpdatedAt == default ? DateTimeOffset.UtcNow : UpdatedAt,
            string.IsNullOrWhiteSpace(DefaultVersionTag)
                ? null
                : VersionTagParser.TryParse(DefaultVersionTag),
            frameSettings.Value);
    }
}
