namespace CutLab.Application.Reporting.ExportMissingCuts;

using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record ExportMissingCutsCommand(
    ProjectId ProjectId,
    Guid SessionId,
    string OutputPath,
    int? ScopeFromCut = null,
    int? ScopeToCut = null);

public sealed record ExportMissingCutsResult(string OutputPath, int RowCount);

public sealed class ExportMissingCutsHandler
{
    private readonly GetMissingCutsFromSessionHandler _getMissingCutsHandler;

    public ExportMissingCutsHandler(GetMissingCutsFromSessionHandler getMissingCutsHandler)
    {
        _getMissingCutsHandler = getMissingCutsHandler;
    }

    public async Task<Result<ExportMissingCutsResult>> HandleAsync(
        ExportMissingCutsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<ExportMissingCutsResult>("导出路径不能为空。");
        }

        var reportResult = await _getMissingCutsHandler.HandleAsync(
            new GetMissingCutsFromSessionQuery(
                command.ProjectId,
                command.SessionId,
                command.ScopeFromCut,
                command.ScopeToCut),
            cancellationToken);

        if (reportResult.IsFailure || reportResult.Value is null)
        {
            return Result.Failure<ExportMissingCutsResult>(reportResult.Error ?? "缺卡检测失败。");
        }

        var format = MissingCutsReportWriter.ResolveFormat(command.OutputPath);
        await MissingCutsReportWriter.WriteAsync(command.OutputPath, reportResult.Value, format, cancellationToken);

        var rowCount = reportResult.Value.MissingCuts.Count + reportResult.Value.MissingInsertSuffixes.Count;
        return Result.Success(new ExportMissingCutsResult(command.OutputPath, rowCount));
    }
}
