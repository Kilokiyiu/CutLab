namespace CutLab.Application.Reporting.ExportMissingCuts;

using System.Globalization;
using System.Text;
using CutLab.Application.Reporting.GetMissingCutsFromSession;

public enum MissingCutsExportFormat
{
    Csv,
    Txt
}

public static class MissingCutsReportWriter
{
    public static async Task WriteAsync(
        string outputPath,
        MissingCutsFromSessionDto report,
        MissingCutsExportFormat format,
        CancellationToken cancellationToken = default)
    {
        var content = format == MissingCutsExportFormat.Csv
            ? BuildCsv(report)
            : BuildTxt(report);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken);
    }

    public static MissingCutsExportFormat ResolveFormat(string outputPath) =>
        Path.GetExtension(outputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? MissingCutsExportFormat.Csv
            : MissingCutsExportFormat.Txt;

    private static string BuildCsv(MissingCutsFromSessionDto report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("类型,卡号,集,场,镜,说明");

        foreach (var missing in report.MissingCuts)
        {
            builder.AppendLine(string.Join(',',
                EscapeCsv("缺卡"),
                EscapeCsv($"C{missing.CutNumber.Cut:D3}"),
                missing.CutNumber.Episode.ToString(CultureInfo.InvariantCulture),
                missing.CutNumber.Scene.ToString(CultureInfo.InvariantCulture),
                missing.CutNumber.Cut.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(missing.Reason)));
        }

        foreach (var missing in report.MissingInsertSuffixes)
        {
            builder.AppendLine(string.Join(',',
                EscapeCsv("缺插卡后缀"),
                EscapeCsv($"C{missing.CutNumber.Cut:D3}{missing.MissingSuffix}"),
                missing.CutNumber.Episode.ToString(CultureInfo.InvariantCulture),
                missing.CutNumber.Scene.ToString(CultureInfo.InvariantCulture),
                missing.CutNumber.Cut.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(missing.Reason)));
        }

        return builder.ToString();
    }

    private static string BuildTxt(MissingCutsFromSessionDto report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"缺卡检测报告 — EP{report.Scope.Episode.Value:D2} S{report.Scope.Scene:D2}");
        builder.AppendLine($"范围：C{report.Scope.From.Cut:D3} - C{report.Scope.To.Cut:D3}");
        builder.AppendLine($"已识别：{report.RegisteredCount} / 期望：{report.TotalExpected}");
        builder.AppendLine();

        if (report.MissingCuts.Count == 0 && report.MissingInsertSuffixes.Count == 0)
        {
            builder.AppendLine("范围内无缺卡。");
            return builder.ToString();
        }

        foreach (var missing in report.MissingCuts)
        {
            builder.AppendLine($"[缺卡] C{missing.CutNumber.Cut:D3} — {missing.Reason}");
        }

        foreach (var missing in report.MissingInsertSuffixes)
        {
            builder.AppendLine($"[缺插卡后缀] C{missing.CutNumber.Cut:D3}{missing.MissingSuffix} — {missing.Reason}");
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
