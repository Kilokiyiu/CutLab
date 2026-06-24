namespace CutLab.Domain.ValueObjects;

using CutLab.Domain.Common;

public sealed record ArchiveTemplate(
    string PathPattern,
    IReadOnlyList<string> FolderNames)
{
    public static Result<ArchiveTemplate> Create(string pathPattern, IReadOnlyList<string> folderNames)
    {
        if (string.IsNullOrWhiteSpace(pathPattern))
        {
            return Result.Failure<ArchiveTemplate>("归档路径模板不能为空。");
        }

        if (folderNames.Count == 0 || folderNames.Any(string.IsNullOrWhiteSpace))
        {
            return Result.Failure<ArchiveTemplate>("归档子目录不能为空。");
        }

        return Result.Success(new ArchiveTemplate(pathPattern, folderNames));
    }
}
