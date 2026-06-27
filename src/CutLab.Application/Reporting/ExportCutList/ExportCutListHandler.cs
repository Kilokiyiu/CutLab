namespace CutLab.Application.Reporting.ExportCutList;

using CutLab.Application.Common;
using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public sealed record ExportCutListCommand(
    ProjectId ProjectId,
    Guid SessionId,
    string OutputPath,
    string? VersionTagFilter = null);

public sealed record ExportCutListResult(string OutputPath, int RowCount);

public sealed class ExportCutListHandler
{
    private readonly IScanSessionRepository _sessionRepository;
    private readonly ICutListExportService _exportService;

    public ExportCutListHandler(IScanSessionRepository sessionRepository, ICutListExportService exportService)
    {
        _sessionRepository = sessionRepository;
        _exportService = exportService;
    }

    public async Task<Result<ExportCutListResult>> HandleAsync(
        ExportCutListCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ExportCutListResult>("扫描会话不存在。");
        }

        if (session.ProjectId != command.ProjectId)
        {
            return Result.Failure<ExportCutListResult>("扫描会话与项目不匹配。");
        }

        if (string.IsNullOrWhiteSpace(command.OutputPath))
        {
            return Result.Failure<ExportCutListResult>("导出路径不能为空。");
        }

        var filteredSession = ScanAssetFilter.CreateFilteredView(session, command.VersionTagFilter);
        var renameItems = RenamePlanBuilder.Build(filteredSession)
            .ToDictionary(item => item.AssetId);

        var rows = filteredSession.Assets
            .OrderBy(asset => asset.ParsedCut?.Cut ?? int.MaxValue)
            .ThenBy(asset => asset.OriginalPath.Value, StringComparer.OrdinalIgnoreCase)
            .Select(asset => MapRow(asset, renameItems.GetValueOrDefault(asset.Id)))
            .ToList();

        await _exportService.ExportAsync(command.OutputPath, rows, cancellationToken);
        return Result.Success(new ExportCutListResult(command.OutputPath, rows.Count));
    }

    private static CutListExportRow MapRow(ProductionAsset asset, RenamePlanItem? renameItem)
    {
        var cut = asset.ParsedCut;
        var cutId = cut is null ? string.Empty : cut.Value.ToString();
        var assetType = asset.AssetType?.ToString() ?? string.Empty;

        return new CutListExportRow(
            cutId,
            cut?.Episode ?? 0,
            cut?.Scene ?? 0,
            cut?.Cut ?? 0,
            assetType,
            Path.GetFileName(asset.OriginalPath.Value),
            asset.OriginalPath.Value,
            asset.RecognitionStatus.ToString(),
            asset.ProposedFileName?.Value ?? string.Empty,
            renameItem?.Status.ToString() ?? string.Empty,
            renameItem?.Message ?? string.Empty,
            asset.VersionTag?.Value ?? string.Empty);
    }
}
