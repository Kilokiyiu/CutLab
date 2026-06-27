namespace CutLab.Application.Common.Interfaces;

public sealed record CutListExportRow(
    string CutId,
    int Episode,
    int Scene,
    int Cut,
    string AssetType,
    string FileName,
    string FilePath,
    string RecognitionStatus,
    string ProposedName,
    string RenameStatus,
    string Note,
    string VersionTag);

public interface ICutListExportService
{
    Task ExportAsync(string outputPath, IReadOnlyList<CutListExportRow> rows, CancellationToken cancellationToken = default);
}
