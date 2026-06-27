namespace CutLab.Infrastructure.Export;

using CutLab.Application.Common.Interfaces;
using MiniExcelLibs;

public sealed class MiniExcelCutListExportService : ICutListExportService
{
    public Task ExportAsync(
        string outputPath,
        IReadOnlyList<CutListExportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var sheetRows = rows.Select(row => new
        {
            卡号 = row.CutId,
            集 = row.Episode,
            场 = row.Scene,
            镜 = row.Cut,
            类型 = row.AssetType,
            文件名 = row.FileName,
            路径 = row.FilePath,
            识别状态 = row.RecognitionStatus,
            建议文件名 = row.ProposedName,
            重命名状态 = row.RenameStatus,
            备注 = row.Note
        });

        MiniExcel.SaveAs(outputPath, sheetRows);
        return Task.CompletedTask;
    }
}
