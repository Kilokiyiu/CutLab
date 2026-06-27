namespace CutLab.Application.Common.Interfaces;

public sealed record ProgressReportRow(
    string CutId,
    int Episode,
    int Scene,
    int Cut,
    string InsertSuffix,
    string HasStoryboard,
    string HasKeyframe,
    string HasInbetween,
    string HasBackground,
    string HasRender,
    int FileCount,
    string ProgressStatus);

public interface IProgressReportExportService
{
    Task ExportAsync(string outputPath, IReadOnlyList<ProgressReportRow> rows, CancellationToken cancellationToken = default);
}
