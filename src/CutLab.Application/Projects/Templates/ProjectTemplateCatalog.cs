namespace CutLab.Application.Projects.Templates;

using System.Text.Json;

public sealed record ProjectTemplateSummary(string Name, string Description);

public sealed class ProjectTemplateDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string NamingTemplate { get; init; }

    public required string ArchivePathPattern { get; init; }

    public required IReadOnlyList<string> ArchiveFolders { get; init; }

    public required IReadOnlyList<string> RecognitionPatterns { get; init; }
}

public interface IProjectTemplateCatalog
{
    IReadOnlyList<ProjectTemplateSummary> List();

    ProjectTemplateDefinition? Get(string name);
}

public sealed class ProjectTemplateCatalog : IProjectTemplateCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _templatesDirectory;
    private IReadOnlyList<ProjectTemplateSummary>? _summaries;
    private Dictionary<string, ProjectTemplateDefinition>? _definitions;

    public ProjectTemplateCatalog(string templatesDirectory)
    {
        _templatesDirectory = templatesDirectory;
    }

    public IReadOnlyList<ProjectTemplateSummary> List()
    {
        EnsureLoaded();
        return _summaries ?? [];
    }

    public ProjectTemplateDefinition? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        EnsureLoaded();
        return _definitions?.GetValueOrDefault(name.Trim());
    }

    private void EnsureLoaded()
    {
        if (_summaries is not null && _definitions is not null)
        {
            return;
        }

        var summaries = new List<ProjectTemplateSummary>();
        var definitions = new Dictionary<string, ProjectTemplateDefinition>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(_templatesDirectory))
        {
            foreach (var file in Directory.GetFiles(_templatesDirectory, "*.json").OrderBy(Path.GetFileName))
            {
                if (TryReadTemplate(file) is not { } template)
                {
                    continue;
                }

                summaries.Add(new ProjectTemplateSummary(template.Name, template.Description));
                definitions[template.Name] = template;
            }
        }

        _summaries = summaries;
        _definitions = definitions;
    }

    private static ProjectTemplateDefinition? TryReadTemplate(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ProjectTemplateFileDto>(json, JsonOptions);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            {
                return null;
            }

            return new ProjectTemplateDefinition
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? dto.Name.Trim(),
                NamingTemplate = dto.NamingRule?.Template?.Trim()
                    ?? "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                ArchivePathPattern = dto.ArchiveTemplate?.PathPattern?.Trim()
                    ?? "{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}/",
                ArchiveFolders = dto.ArchiveTemplate?.Folders?.Where(folder => !string.IsNullOrWhiteSpace(folder)).ToList()
                    ?? ["分镜", "原画", "动画", "背景", "渲染"],
                RecognitionPatterns = dto.RecognitionPatterns?
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(pattern => pattern.Trim())
                    .ToList() ?? []
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ProjectTemplateFileDto
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ProjectTemplateNamingRuleDto? NamingRule { get; set; }

        public ProjectTemplateArchiveDto? ArchiveTemplate { get; set; }

        public List<string>? RecognitionPatterns { get; set; }
    }

    private sealed class ProjectTemplateNamingRuleDto
    {
        public string? Template { get; set; }
    }

    private sealed class ProjectTemplateArchiveDto
    {
        public string? PathPattern { get; set; }

        public List<string>? Folders { get; set; }
    }
}
