namespace CutLab.Application.Reporting.ExportFrameSequenceIssues;

using System.Text;
using CutLab.Application.Reporting.AnalyzeFrameSequences;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed record ExportFrameSequenceIssuesCommand(
    ProjectId ProjectId,
    Guid SessionId,
    string OutputPath);

public sealed record ExportFrameSequenceIssuesResult(string OutputPath, int RowCount);

public sealed class ExportFrameSequenceIssuesHandler
{
    private readonly AnalyzeFrameSequencesFromSessionHandler _analyzeHandler;

    public ExportFrameSequenceIssuesHandler(AnalyzeFrameSequencesFromSessionHandler analyzeHandler)
    {
        _analyzeHandler = analyzeHandler;
    }

    public async Task<Result<ExportFrameSequenceIssuesResult>> HandleAsync(
        ExportFrameSequenceIssuesCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<ExportFrameSequenceIssuesResult>("导出路径不能为空。");
        }

        var analysis = await _analyzeHandler.HandleAsync(
            new AnalyzeFrameSequencesFromSessionQuery(command.ProjectId, command.SessionId),
            cancellationToken);

        if (analysis.IsFailure || analysis.Value is null)
        {
            return Result.Failure<ExportFrameSequenceIssuesResult>(analysis.Error ?? "帧序列分析失败。");
        }

        var rowCount = await FrameSequenceReportWriter.WriteAsync(command.OutputPath, analysis.Value, cancellationToken);
        return Result.Success(new ExportFrameSequenceIssuesResult(command.OutputPath, rowCount));
    }
}

internal static class FrameSequenceReportWriter
{
    public static async Task<int> WriteAsync(
        string outputPath,
        FrameSequenceAnalysisResult result,
        CancellationToken cancellationToken)
    {
        var rows = BuildRows(result);
        var extension = Path.GetExtension(outputPath).ToLowerInvariant();

        if (extension == ".csv")
        {
            await WriteCsvAsync(outputPath, rows, cancellationToken);
        }
        else
        {
            await WriteTextAsync(outputPath, rows, cancellationToken);
        }

        return rows.Count;
    }

    private static List<(string Category, string Detail, string Reason)> BuildRows(FrameSequenceAnalysisResult result)
    {
        var rows = new List<(string, string, string)>();

        foreach (var issue in result.MissingFrames)
        {
            rows.Add(("缺帧", $"C{issue.CutNumber.Cut:D3} / 帧 {issue.FrameNumber:D3}", issue.Reason));
        }

        foreach (var issue in result.DuplicateFrames)
        {
            rows.Add(("重复帧", $"C{issue.CutNumber.Cut:D3} / 帧 {issue.FrameNumber:D3}", string.Join("; ", issue.FilePaths)));
        }

        foreach (var issue in result.CrossCutFrames)
        {
            rows.Add(("跨卡帧", $"帧 {issue.FrameNumber:D3}", string.Join("; ", issue.Entries)));
        }

        foreach (var issue in result.OrphanFrames)
        {
            rows.Add(("孤立帧", issue.FilePath, issue.Reason));
        }

        return rows;
    }

    private static async Task WriteCsvAsync(
        string outputPath,
        IReadOnlyList<(string Category, string Detail, string Reason)> rows,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("类型,详情,说明");
        foreach (var row in rows)
        {
            builder.AppendLine($"{EscapeCsv(row.Category)},{EscapeCsv(row.Detail)},{EscapeCsv(row.Reason)}");
        }

        await File.WriteAllTextAsync(outputPath, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static async Task WriteTextAsync(
        string outputPath,
        IReadOnlyList<(string Category, string Detail, string Reason)> rows,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine($"[{row.Category}] {row.Detail}");
            builder.AppendLine(row.Reason);
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, builder.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
