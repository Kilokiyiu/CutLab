namespace CutLab.Infrastructure.Export;

using CutLab.Application.Common.Interfaces;
using MiniExcelLibs;

public sealed class MiniExcelProgressReportExportService : IProgressReportExportService
{
    public Task ExportAsync(
        string outputPath,
        IReadOnlyList<ProgressReportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var sheetRows = rows.Select(row => new
        {
            卡号 = row.CutId,
            集 = row.Episode,
            场 = row.Scene,
            镜 = row.Cut,
            插卡 = row.InsertSuffix,
            分镜 = row.HasStoryboard,
            原画 = row.HasKeyframe,
            动画 = row.HasInbetween,
            背景 = row.HasBackground,
            渲染 = row.HasRender,
            文件数 = row.FileCount,
            进度状态 = row.ProgressStatus
        });

        MiniExcel.SaveAs(outputPath, sheetRows);
        return Task.CompletedTask;
    }
}
