namespace CutLab.Application.Projects.ProjectConfig;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class ProjectConfigSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<ProjectConfigDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ProjectConfigDocument>(stream, JsonOptions, cancellationToken);
        if (document is null)
        {
            throw new InvalidDataException("项目配置文件为空或格式无效。");
        }

        return document;
    }

    public static async Task WriteAsync(string path, ProjectConfigDocument document, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
    }
}
