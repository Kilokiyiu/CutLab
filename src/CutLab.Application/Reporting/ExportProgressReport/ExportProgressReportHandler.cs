namespace CutLab.Application.Reporting.ExportProgressReport;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;

public sealed record ExportProgressReportCommand(
    ProjectId ProjectId,
    Guid SessionId,
    string OutputPath,
    string? VersionTagFilter = null);

public sealed record ExportProgressReportResult(string OutputPath, int RowCount);

public sealed class ExportProgressReportHandler
{
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IProgressReportExportService _exportService;

    public ExportProgressReportHandler(
        IScanSessionRepository sessionRepository,
        IProgressReportExportService exportService)
    {
        _sessionRepository = sessionRepository;
        _exportService = exportService;
    }

    public async Task<Result<ExportProgressReportResult>> HandleAsync(
        ExportProgressReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ExportProgressReportResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<ExportProgressReportResult>("扫描会话与项目不匹配。");
        }

        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<ExportProgressReportResult>("导出路径不能为空。");
        }

        var filteredSession = ScanAssetFilter.CreateFilteredView(session, command.VersionTagFilter);
        var rows = CutProgressAnalyzer.Analyze(filteredSession)
            .Select(MapRow)
            .ToList();

        if (rows.Count == 0)
        {
            return Result.Failure<ExportProgressReportResult>("没有可导出的进度数据。");
        }

        await _exportService.ExportAsync(command.OutputPath, rows, cancellationToken);
        return Result.Success(new ExportProgressReportResult(command.OutputPath, rows.Count));
    }

    private static ProgressReportRow MapRow(CutProgressRow row) =>
        new(
            row.CutId,
            row.Episode,
            row.Scene,
            row.Cut,
            row.InsertSuffix ?? string.Empty,
            ToMark(row.HasStoryboard),
            ToMark(row.HasKeyframe),
            ToMark(row.HasInbetween),
            ToMark(row.HasBackground),
            ToMark(row.HasRender),
            row.FileCount,
            row.ProgressStatus);

    private static string ToMark(bool value) => value ? "✓" : "—";
}
