namespace CutLab.App.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutLab.App.Services;
using CutLab.Application.Common;
using CutLab.Application.Operations.BatchAutoNumberUnrecognized;
using CutLab.Application.Operations.ExecuteArchive;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Operations.InsertCut;
using CutLab.Application.Operations.ReorderCuts;
using CutLab.Application.Operations.UndoLastOperation;
using CutLab.Application.Projects.GetProject;
using CutLab.Application.Projects.ProjectConfig;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.ListRecentProjects;
using CutLab.Application.Reporting.ExportCutList;
using CutLab.Application.Reporting.ExportProgressReport;
using CutLab.Application.Reporting.GeneratePreviewVideo;
using CutLab.Application.Reporting.ExportMissingCuts;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Projects.DeleteProject;
using CutLab.Application.Scanning.GetScanPreview;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;
using Avalonia.Media.Imaging;
using Avalonia.Controls;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IWindowService _windowService;
    private readonly ListRecentProjectsHandler _listRecentProjectsHandler;
    private readonly CreateProjectHandler _createProjectHandler;
    private readonly ScanFolderHandler _scanFolderHandler;
    private readonly GetScanPreviewHandler _getScanPreviewHandler;
    private readonly ExecuteRenameHandler _executeRenameHandler;
    private readonly ExecuteArchiveHandler _executeArchiveHandler;
    private readonly ExportCutListHandler _exportCutListHandler;
    private readonly GetMissingCutsFromSessionHandler _getMissingCutsFromSessionHandler;
    private readonly UndoLastOperationHandler _undoLastOperationHandler;
    private readonly InsertCutHandler _insertCutHandler;
    private readonly BatchAutoNumberUnrecognizedHandler _batchAutoNumberHandler;
    private readonly ReorderCutsHandler _reorderCutsHandler;
    private readonly GeneratePreviewVideoHandler _generatePreviewVideoHandler;
    private readonly ExportProgressReportHandler _exportProgressReportHandler;
    private readonly GetProjectHandler _getProjectHandler;
    private readonly ExportProjectConfigHandler _exportProjectConfigHandler;
    private readonly ImportProjectConfigHandler _importProjectConfigHandler;
    private readonly ExportMissingCutsHandler _exportMissingCutsHandler;
    private readonly DeleteProjectHandler _deleteProjectHandler;
    private readonly IUserPreferencesStore _userPreferencesStore;

    private ProjectId? _currentProjectId;
    private Guid? _currentSessionId;
    private bool _suppressProjectSelectionChange;

    public MainWindowViewModel(
        IFileDialogService fileDialogService,
        IWindowService windowService,
        ListRecentProjectsHandler listRecentProjectsHandler,
        CreateProjectHandler createProjectHandler,
        ScanFolderHandler scanFolderHandler,
        GetScanPreviewHandler getScanPreviewHandler,
        ExecuteRenameHandler executeRenameHandler,
        ExecuteArchiveHandler executeArchiveHandler,
        ExportCutListHandler exportCutListHandler,
        GetMissingCutsFromSessionHandler getMissingCutsFromSessionHandler,
        UndoLastOperationHandler undoLastOperationHandler,
        InsertCutHandler insertCutHandler,
        BatchAutoNumberUnrecognizedHandler batchAutoNumberHandler,
        ReorderCutsHandler reorderCutsHandler,
        GeneratePreviewVideoHandler generatePreviewVideoHandler,
        ExportProgressReportHandler exportProgressReportHandler,
        GetProjectHandler getProjectHandler,
        ExportProjectConfigHandler exportProjectConfigHandler,
        ImportProjectConfigHandler importProjectConfigHandler,
        ExportMissingCutsHandler exportMissingCutsHandler,
        DeleteProjectHandler deleteProjectHandler,
        IUserPreferencesStore userPreferencesStore)
    {
        _fileDialogService = fileDialogService;
        _windowService = windowService;
        _listRecentProjectsHandler = listRecentProjectsHandler;
        _createProjectHandler = createProjectHandler;
        _scanFolderHandler = scanFolderHandler;
        _getScanPreviewHandler = getScanPreviewHandler;
        _executeRenameHandler = executeRenameHandler;
        _executeArchiveHandler = executeArchiveHandler;
        _exportCutListHandler = exportCutListHandler;
        _getMissingCutsFromSessionHandler = getMissingCutsFromSessionHandler;
        _undoLastOperationHandler = undoLastOperationHandler;
        _insertCutHandler = insertCutHandler;
        _batchAutoNumberHandler = batchAutoNumberHandler;
        _reorderCutsHandler = reorderCutsHandler;
        _generatePreviewVideoHandler = generatePreviewVideoHandler;
        _exportProgressReportHandler = exportProgressReportHandler;
        _getProjectHandler = getProjectHandler;
        _exportProjectConfigHandler = exportProjectConfigHandler;
        _importProjectConfigHandler = importProjectConfigHandler;
        _exportMissingCutsHandler = exportMissingCutsHandler;
        _deleteProjectHandler = deleteProjectHandler;
        _userPreferencesStore = userPreferencesStore;

        var columnPreferences = _userPreferencesStore.Load();
        _shotColCutWidth = new GridLength(Math.Max(48, columnPreferences.ShotColCutWidth));
        _shotColSourceWidth = new GridLength(Math.Max(64, columnPreferences.ShotColSourceWidth));
        _shotColTargetWidth = new GridLength(Math.Max(64, columnPreferences.ShotColTargetWidth));
        _shotColStatusWidth = new GridLength(Math.Max(48, columnPreferences.ShotColStatusWidth));

        PreviewItems = [];
        PreviewItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShotListTitle));
        RecentProjects = [];
        CutGallery = [];
        _ = InitializeAsync();
    }

    public string Title { get; } = "CutLab";

    public string Subtitle { get; } = "多项目 · 重命名 · 插卡 · 归档 · 进度台账 · 预览";

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _versionTagFilter = string.Empty;

    [ObservableProperty]
    private int _insertAfterCut = 1;

    [ObservableProperty]
    private int _missingCutFrom;

    [ObservableProperty]
    private int _missingCutTo;

    [ObservableProperty]
    private AssetType _insertAssetType = AssetType.Keyframe;

    [ObservableProperty]
    private bool _insertUnrecognizedOnly = true;

    [ObservableProperty]
    private string _statusMessage = "请选择文件夹并扫描。";

    [ObservableProperty]
    private string _missingCutsSummary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _scanRecursive;

    [ObservableProperty]
    private ProjectListItemViewModel? _selectedProject;

    [ObservableProperty]
    private PreviewRowViewModel? _selectedPreviewRow;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private string _previewCaption = "选择左侧列表中的文件以预览。";

    [ObservableProperty]
    private string _galleryHint = string.Empty;

    [ObservableProperty]
    private bool _showGalleryHint;

    [ObservableProperty]
    private bool _isReorderDragging;

    [ObservableProperty]
    private string _reorderDragHint = string.Empty;

    [ObservableProperty]
    private GridLength _shotColCutWidth = new(90);

    [ObservableProperty]
    private GridLength _shotColSourceWidth = new(200);

    [ObservableProperty]
    private GridLength _shotColTargetWidth = new(200);

    [ObservableProperty]
    private GridLength _shotColStatusWidth = new(80);

    public void PersistColumnWidths()
    {
        _userPreferencesStore.Save(new UserPreferences
        {
            ShotColCutWidth = ShotColCutWidth.Value,
            ShotColSourceWidth = ShotColSourceWidth.Value,
            ShotColTargetWidth = ShotColTargetWidth.Value,
            ShotColStatusWidth = ShotColStatusWidth.Value
        });
    }

    partial void OnShotColCutWidthChanged(GridLength value) => PersistColumnWidths();

    partial void OnShotColSourceWidthChanged(GridLength value) => PersistColumnWidths();

    partial void OnShotColTargetWidthChanged(GridLength value) => PersistColumnWidths();

    partial void OnShotColStatusWidthChanged(GridLength value) => PersistColumnWidths();

    public ObservableCollection<PreviewRowViewModel> PreviewItems { get; }

    public ObservableCollection<ProjectListItemViewModel> RecentProjects { get; }

    public ObservableCollection<CutGalleryItemViewModel> CutGallery { get; }

    public string ShotListTitle =>
        PreviewItems.Count == 0
            ? "镜头清单"
            : $"镜头清单（{PreviewItems.Count}）· 拖拽行调整卡号顺序";

    public async Task ReorderCutAsync(int movedCut, int targetIndexInOriginal)
    {
        if (_currentProjectId is null || _currentSessionId is null || IsBusy)
        {
            return;
        }

        var targetIndex = AdjustReorderTargetIndex(movedCut, targetIndexInOriginal);
        if (!WouldReorderChange(movedCut, targetIndexInOriginal))
        {
            return;
        }

        await RunOperationAsync($"正在调整卡号 C{movedCut:D3}...", async () =>
        {
            var preview = await _reorderCutsHandler.HandleAsync(new ReorderCutsCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                movedCut,
                targetIndex,
                DryRun: true));

            if (preview.IsFailure || preview.Value is null)
            {
                return preview.Error ?? "卡号重排预览失败。";
            }

            PreviewItems.Clear();
            foreach (var item in preview.Value.Items)
            {
                PreviewItems.Add(PreviewRowViewModel.FromRename(item));
            }

            if (preview.Value.ReadyCount == 0)
            {
                return "卡号顺序未发生变化。";
            }

            var result = await _reorderCutsHandler.HandleAsync(new ReorderCutsCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                movedCut,
                targetIndex,
                DryRun: false));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "卡号重排失败。";
            }

            await RescanAsync();
            return $"卡号重排完成：重命名 {result.Value.RenamedCount} 个文件。";
        });
    }

    public int GetTargetIndexForCut(int targetCut)
    {
        var orderedCuts = GetOrderedCuts().ToList();
        var index = orderedCuts.IndexOf(targetCut);
        return index < 0 ? orderedCuts.Count : index;
    }

    public int AdjustReorderTargetIndex(int movedCut, int targetIndexInOriginal)
    {
        var orderedCuts = GetOrderedCuts().ToList();
        var fromIndex = orderedCuts.IndexOf(movedCut);
        if (fromIndex < 0)
        {
            return targetIndexInOriginal;
        }

        return fromIndex < targetIndexInOriginal
            ? targetIndexInOriginal - 1
            : targetIndexInOriginal;
    }

    public bool WouldReorderChange(int movedCut, int targetIndexInOriginal)
    {
        var original = GetOrderedCuts();
        var orderedCuts = original.ToList();
        var fromIndex = orderedCuts.IndexOf(movedCut);
        if (fromIndex < 0)
        {
            return false;
        }

        var targetIndex = AdjustReorderTargetIndex(movedCut, targetIndexInOriginal);
        orderedCuts.RemoveAt(fromIndex);
        targetIndex = Math.Clamp(targetIndex, 0, orderedCuts.Count);
        orderedCuts.Insert(targetIndex, movedCut);
        return !orderedCuts.SequenceEqual(original);
    }

    public string BuildReorderDragHint(int movedCut, int targetIndexInOriginal, bool insertAfter, int? anchorCut)
    {
        var original = GetOrderedCuts();
        if (original.Count == 0 || !original.Contains(movedCut))
        {
            return string.Empty;
        }

        if (!WouldReorderChange(movedCut, targetIndexInOriginal))
        {
            return "松开后顺序不变";
        }

        var orderedCuts = original.ToList();
        var fromIndex = orderedCuts.IndexOf(movedCut);
        var targetIndex = AdjustReorderTargetIndex(movedCut, targetIndexInOriginal);
        orderedCuts.RemoveAt(fromIndex);
        targetIndex = Math.Clamp(targetIndex, 0, orderedCuts.Count);
        orderedCuts.Insert(targetIndex, movedCut);

        var startCut = original.Min();
        var newCutNumber = startCut + targetIndex;
        var positionText = anchorCut is int cut
            ? insertAfter
                ? $"插入到 C{cut:D3} 之后"
                : $"插入到 C{cut:D3} 之前"
            : "移动到末尾";

        return $"松开应用：C{movedCut:D3} → C{newCutNumber:D3}（{positionText}）";
    }

    public void UpdateReorderDragPreview(int movedCut, int targetIndexInOriginal, bool insertAfter, int? anchorCut)
    {
        IsReorderDragging = true;
        ReorderDragHint = BuildReorderDragHint(movedCut, targetIndexInOriginal, insertAfter, anchorCut);
    }

    public void ClearReorderDragPreview()
    {
        IsReorderDragging = false;
        ReorderDragHint = string.Empty;
    }

    private IReadOnlyList<int> GetOrderedCuts() =>
        PreviewItems
            .Where(row => row.ReorderCut.HasValue)
            .Select(row => row.ReorderCut!.Value)
            .Distinct()
            .OrderBy(cut => cut)
            .ToList();

    public AssetType[] AvailableAssetTypes { get; } = Enum.GetValues<AssetType>();

    [RelayCommand(CanExecute = nameof(CanManageProjects))]
    private async Task RefreshProjectsAsync()
    {
        await RefreshRecentProjectsAsync();
        StatusMessage = $"已刷新项目列表（{RecentProjects.Count} 个）。";
    }

    [RelayCommand(CanExecute = nameof(CanManageProjects))]
    private async Task NewProjectAsync()
    {
        var folder = await _fileDialogService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var projectName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(projectName))
            {
                projectName = "CutLab Project";
            }

            var archiveRoot = Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                ?? folder;

            var result = await _createProjectHandler.HandleAsync(new CreateProjectCommand(
                projectName,
                Episode: 1,
                NamingTemplate: "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
                ArchivePathPattern: "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
                ArchiveFolders: ["分镜", "原画", "动画", "背景", "渲染"],
                RootPath: archiveRoot));

            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "新建项目失败。";
                return;
            }

            _currentProjectId = result.Value;
            SourcePath = folder;
            _currentSessionId = null;
            PreviewItems.Clear();
            CutGallery.Clear();
            SelectedPreviewRow = null;
            MissingCutsSummary = string.Empty;

            await RefreshRecentProjectsAsync();
            StatusMessage = $"已新建项目：{projectName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"新建项目出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand]
    private void SelectGalleryCut(string? cutId)
    {
        if (string.IsNullOrWhiteSpace(cutId))
        {
            return;
        }

        SelectedPreviewRow = PreviewItems.FirstOrDefault(row => row.CutId == cutId && row.IsImage)
                           ?? PreviewItems.FirstOrDefault(row => row.CutId == cutId);
    }

    [RelayCommand(CanExecute = nameof(CanPickFolder))]
    private async Task PickFolderAsync()
    {
        var folder = await _fileDialogService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SourcePath = folder;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            StatusMessage = "请先输入有效的文件夹路径。";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在扫描...";
            MissingCutsSummary = string.Empty;

            var projectId = await EnsureProjectAsync();
            if (projectId is null)
            {
                StatusMessage = "项目创建失败。";
                return;
            }

            var scanResult = await _scanFolderHandler.HandleAsync(
                new ScanFolderCommand(projectId.Value, SourcePath, ScanRecursive));

            if (scanResult.IsFailure || scanResult.Value is null)
            {
                StatusMessage = scanResult.Error ?? "扫描失败。";
                return;
            }

            _currentProjectId = projectId;
            _currentSessionId = scanResult.Value.SessionId;

            await RefreshRecentProjectsAsync();
            await LoadRenamePreviewAsync();
            await LoadMissingCutsAsync();

            _versionFilterApplied = false;
            var recursiveHint = ScanRecursive ? "（含子目录）" : string.Empty;
            StatusMessage =
                $"扫描完成{recursiveHint}：{scanResult.Value.TotalFiles} 个文件，" +
                $"识别 {scanResult.Value.RecognizedCount}，" +
                $"未识别 {scanResult.Value.UnrecognizedCount}。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"扫描出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private async Task OpenSettingsAsync()
    {
        var projectId = await EnsureProjectAsync();
        if (projectId is null)
        {
            StatusMessage = "请先创建或加载项目。";
            return;
        }

        _currentProjectId = projectId;
        var saved = await _windowService.ShowProjectSettingsAsync(projectId.Value);
        StatusMessage = saved ? "项目设置已保存。" : "项目设置未更改。";
    }

    [RelayCommand(CanExecute = nameof(CanManageProjectConfig))]
    private async Task ExportProjectConfigAsync()
    {
        var projectId = await EnsureProjectAsync();
        if (projectId is null)
        {
            StatusMessage = "请先创建或加载项目。";
            return;
        }

        _currentProjectId = projectId;
        var project = await _getProjectHandler.HandleAsync(new GetProjectQuery(projectId.Value));
        var suggestedName = project.IsSuccess && project.Value is not null
            ? $"{SanitizeFileName(project.Value.Name)}.cutlab.json"
            : "CutLab-Project.cutlab.json";

        var outputPath = await _fileDialogService.SaveProjectConfigFileAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在导出项目配置...";

            var result = await _exportProjectConfigHandler.HandleAsync(new ExportProjectConfigCommand(
                projectId.Value,
                outputPath,
                IncludeRootPath: true));

            StatusMessage = result.IsSuccess
                ? $"已导出项目配置：{outputPath}"
                : result.Error ?? "导出项目配置失败。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出项目配置出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanImportProjectConfig))]
    private async Task ImportProjectConfigAsync()
    {
        var inputPath = await _fileDialogService.OpenProjectConfigFileAsync();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在导入项目配置...";

            var fallbackRoot = string.IsNullOrWhiteSpace(SourcePath)
                ? null
                : Path.GetDirectoryName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                  ?? SourcePath;

            var result = await _importProjectConfigHandler.HandleAsync(new ImportProjectConfigCommand(
                inputPath,
                _currentProjectId,
                fallbackRoot));

            if (result.IsFailure || result.Value is null)
            {
                StatusMessage = result.Error ?? "导入项目配置失败。";
                return;
            }

            _currentProjectId = result.Value.ProjectId;
            _currentSessionId = null;
            PreviewItems.Clear();
            CutGallery.Clear();
            SelectedPreviewRow = null;
            MissingCutsSummary = string.Empty;

            await RefreshRecentProjectsAsync();
            SelectedProject = RecentProjects.FirstOrDefault(project => project.Id == result.Value.ProjectId);

            var projectDetails = await _getProjectHandler.HandleAsync(new GetProjectQuery(result.Value.ProjectId));
            if (projectDetails.IsSuccess && projectDetails.Value is not null && !string.IsNullOrWhiteSpace(projectDetails.Value.RootPath))
            {
                SourcePath = projectDetails.Value.RootPath;
            }

            StatusMessage = result.Value.CreatedNew
                ? $"已导入并新建项目：{result.Value.ProjectName}"
                : $"已导入项目配置到当前项目：{result.Value.ProjectName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入项目配置出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "CutLab-Project" : name.Trim();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportProgressReportAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        var outputPath = await _fileDialogService.SaveExcelFileAsync("CutLab_进度台账.xlsx");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在导出进度台账...";

            var filter = GetActiveVersionFilter();
            var result = await _exportProgressReportHandler.HandleAsync(new ExportProgressReportCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                outputPath,
                filter));

            StatusMessage = result.IsSuccess
                ? $"已导出 {result.Value!.RowCount} 行进度台账到 {outputPath}"
                : result.Error ?? "导出进度台账失败。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出进度台账出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCutListAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        var outputPath = await _fileDialogService.SaveExcelFileAsync("CutLab_卡号清单.xlsx");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在导出 Excel...";

            var filter = GetActiveVersionFilter();
            var result = await _exportCutListHandler.HandleAsync(new ExportCutListCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                outputPath,
                filter));

            StatusMessage = result.IsSuccess
                ? $"已导出 {result.Value!.RowCount} 行到 {outputPath}"
                : result.Error ?? "导出失败。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInsertCut))]
    private async Task InsertCutAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        await RunOperationAsync("正在执行插卡重命名...", async () =>
        {
            var preview = await _insertCutHandler.HandleAsync(new InsertCutCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                InsertAfterCut,
                InsertAssetType,
                InsertUnrecognizedOnly,
                DryRun: true));

            if (preview.IsFailure || preview.Value is null)
            {
                return preview.Error ?? "插卡预览失败。";
            }

            PreviewItems.Clear();
            foreach (var item in preview.Value.Items)
            {
                PreviewItems.Add(PreviewRowViewModel.FromRename(item));
            }

            if (preview.Value.ReadyCount == 0)
            {
                return $"插卡 {preview.Value.InsertCutNumber}：没有可重命名的文件。";
            }

            var result = await _insertCutHandler.HandleAsync(new InsertCutCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                InsertAfterCut,
                InsertAssetType,
                InsertUnrecognizedOnly,
                DryRun: false));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "插卡重命名失败。";
            }

            await RescanAsync();
            return $"插卡 {result.Value.InsertCutNumber} 完成：重命名 {result.Value.RenamedCount} 个文件。";
        });
    }

    [RelayCommand(CanExecute = nameof(CanBatchAutoNumber))]
    private async Task BatchAutoNumberAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        await RunOperationAsync("正在自动编号重命名...", async () =>
        {
            var preview = await _batchAutoNumberHandler.HandleAsync(new BatchAutoNumberUnrecognizedCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                InsertAfterCut,
                InsertAssetType,
                DryRun: true));

            if (preview.IsFailure || preview.Value is null)
            {
                return preview.Error ?? "自动编号预览失败。";
            }

            PreviewItems.Clear();
            foreach (var item in preview.Value.Items)
            {
                PreviewItems.Add(PreviewRowViewModel.FromRename(item));
            }

            if (preview.Value.ReadyCount == 0)
            {
                return "没有可自动编号的未识别文件。";
            }

            var result = await _batchAutoNumberHandler.HandleAsync(new BatchAutoNumberUnrecognizedCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                InsertAfterCut,
                InsertAssetType,
                DryRun: false));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "自动编号重命名失败。";
            }

            await RescanAsync();
            return $"自动编号完成：C{result.Value.StartCutNumber:D3}–C{result.Value.EndCutNumber:D3}，重命名 {result.Value.RenamedCount} 个文件。";
        });
    }

    [RelayCommand(CanExecute = nameof(CanGeneratePreview))]
    private async Task GeneratePreviewVideoAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        var outputPath = await _fileDialogService.SaveVideoFileAsync("CutLab_预览.mp4");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在生成预览视频（FFmpeg）...";

            var result = await _generatePreviewVideoHandler.HandleAsync(new GeneratePreviewVideoCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                outputPath,
                SecondsPerCut: 2.0,
                PreferredType: AssetType.Storyboard,
                GetActiveVersionFilter()));

            StatusMessage = result.IsSuccess
                ? $"预览视频已生成：{result.Value!.FrameCount} 帧 → {outputPath}"
                : result.Error ?? "预览视频生成失败。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成预览视频出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyVersionFilter))]
    private async Task ApplyVersionFilterAsync()
    {
        _versionFilterApplied = !string.IsNullOrWhiteSpace(VersionTagFilter);
        await LoadRenamePreviewAsync();
        StatusMessage = _versionFilterApplied
            ? $"已筛选版本标签：{VersionTagFilter.Trim()}"
            : "已显示全部文件。";
    }

    [RelayCommand(CanExecute = nameof(CanExecuteRename))]
    private async Task ExecuteRenameAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        await RunOperationAsync("正在执行重命名...", async () =>
        {
            var result = await _executeRenameHandler.HandleAsync(
                new ExecuteRenameCommand(_currentProjectId.Value, _currentSessionId.Value, DryRun: false));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "重命名失败。";
            }

            await RescanAsync();
            return $"重命名完成：成功 {result.Value.RenamedCount}，跳过 {result.Value.SkippedCount}。";
        });
    }

    [RelayCommand(CanExecute = nameof(CanExecuteArchive))]
    private async Task CreateArchiveDirectoriesAsync()
    {
        await ExecuteArchiveAsync(ArchiveExecutionMode.CreateDirectoriesOnly, "正在创建归档目录...");
    }

    [RelayCommand(CanExecute = nameof(CanExecuteArchive))]
    private async Task MoveToArchiveAsync()
    {
        await ExecuteArchiveAsync(ArchiveExecutionMode.MoveFiles, "正在移动文件到归档目录...");
    }

    [RelayCommand(CanExecute = nameof(CanDetectMissingCuts))]
    private async Task DetectMissingCutsAsync()
    {
        await LoadMissingCutsAsync(showFailureAsStatus: true);
    }

    [RelayCommand(CanExecute = nameof(CanExportMissingCuts))]
    private async Task ExportMissingCutsAsync()
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        var outputPath = await _fileDialogService.SaveMissingCutsFileAsync("CutLab_缺卡清单.csv");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在导出缺卡清单...";

            var result = await _exportMissingCutsHandler.HandleAsync(new ExportMissingCutsCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                outputPath,
                ResolveMissingCutScope(MissingCutFrom),
                ResolveMissingCutScope(MissingCutTo)));

            StatusMessage = result.IsSuccess
                ? $"已导出缺卡清单（{result.Value!.RowCount} 项）到 {outputPath}"
                : result.Error ?? "导出缺卡清单失败。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出缺卡清单出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteProject))]
    private async Task DeleteProjectAsync()
    {
        if (_currentProjectId is null)
        {
            return;
        }

        var projectName = SelectedProject?.Name ?? "当前项目";
        var confirmed = await _windowService.ConfirmAsync(
            "删除项目",
            $"确定删除 CutLab 项目「{projectName}」吗？\n\n仅删除项目配置，不会删除磁盘上的素材文件。");

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var deletingId = _currentProjectId.Value;
            var result = await _deleteProjectHandler.HandleAsync(new DeleteProjectCommand(deletingId));
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "删除项目失败。";
                return;
            }

            _currentProjectId = null;
            _currentSessionId = null;
            PreviewItems.Clear();
            CutGallery.Clear();
            SelectedPreviewRow = null;
            MissingCutsSummary = string.Empty;

            await RefreshRecentProjectsAsync();
            if (RecentProjects.Count > 0)
            {
                await ApplyProjectSwitchAsync(RecentProjects[0], updateSelection: true);
                StatusMessage = $"已删除项目「{projectName}」，已切换到 {RecentProjects[0].Name}。";
            }
            else
            {
                SourcePath = string.Empty;
                StatusMessage = $"已删除项目「{projectName}」。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除项目出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        if (_currentProjectId is null)
        {
            return;
        }

        await RunOperationAsync("正在撤销...", async () =>
        {
            var result = await _undoLastOperationHandler.HandleAsync(
                new UndoLastOperationCommand(_currentProjectId.Value));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "撤销失败。";
            }

            await RescanAsync();
            return $"已撤销 {result.Value.RevertedCount} 项操作。";
        });
    }

    private async Task ExecuteArchiveAsync(ArchiveExecutionMode mode, string progressMessage)
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            return;
        }

        await RunOperationAsync(progressMessage, async () =>
        {
            var preview = await _executeArchiveHandler.HandleAsync(new ExecuteArchiveCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                mode,
                DryRun: true));

            if (preview.IsFailure || preview.Value is null)
            {
                return preview.Error ?? "归档预览失败。";
            }

            UpdatePreviewFromArchive(preview.Value.Items);

            var result = await _executeArchiveHandler.HandleAsync(new ExecuteArchiveCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                mode,
                DryRun: false));

            if (result.IsFailure || result.Value is null)
            {
                return result.Error ?? "归档失败。";
            }

            await RescanAsync();
            var action = mode == ArchiveExecutionMode.CreateDirectoriesOnly ? "创建目录" : "移动文件";
            return $"{action}完成：成功 {result.Value.ExecutedCount}，跳过 {result.Value.SkippedCount}。";
        });
    }

    private async Task RunOperationAsync(string progressMessage, Func<Task<string>> operation)
    {
        try
        {
            IsBusy = true;
            StatusMessage = progressMessage;
            StatusMessage = await operation();
        }
        catch (Exception ex)
        {
            StatusMessage = $"操作出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsChanged();
        }
    }

    private async Task RescanAsync()
    {
        if (_currentProjectId is null)
        {
            return;
        }

        var rescan = await _scanFolderHandler.HandleAsync(
            new ScanFolderCommand(_currentProjectId.Value, SourcePath, ScanRecursive));

        if (rescan.IsSuccess && rescan.Value is not null)
        {
            _currentSessionId = rescan.Value.SessionId;
            await LoadRenamePreviewAsync();
            await LoadMissingCutsAsync();
        }
    }

    private async Task InitializeAsync()
    {
        await RefreshRecentProjectsAsync();
        if (RecentProjects.Count > 0)
        {
            await ApplyProjectSwitchAsync(RecentProjects[0], updateSelection: true);
        }
    }

    private async Task RefreshRecentProjectsAsync()
    {
        _suppressProjectSelectionChange = true;
        try
        {
            RecentProjects.Clear();
            var projects = await _listRecentProjectsHandler.HandleAsync(new ListRecentProjectsQuery(10));
            foreach (var project in projects)
            {
                RecentProjects.Add(new ProjectListItemViewModel(
                    project.Id,
                    project.Name,
                    project.RootPath,
                    project.Episode));
            }

            if (_currentProjectId is not null)
            {
                SelectedProject = RecentProjects.FirstOrDefault(project => project.Id == _currentProjectId);
            }
        }
        finally
        {
            _suppressProjectSelectionChange = false;
        }
    }

    private async Task ApplyProjectSwitchAsync(ProjectListItemViewModel project, bool updateSelection = false)
    {
        _currentProjectId = project.Id;
        _currentSessionId = null;
        _versionFilterApplied = false;
        PreviewItems.Clear();
        CutGallery.Clear();
        SelectedPreviewRow = null;
        MissingCutsSummary = string.Empty;

        var projectDetails = await _getProjectHandler.HandleAsync(new GetProjectQuery(project.Id));
        SourcePath = projectDetails.IsSuccess && projectDetails.Value is not null
            ? project.RootPath
            : project.RootPath;

        if (updateSelection)
        {
            _suppressProjectSelectionChange = true;
            SelectedProject = project;
            _suppressProjectSelectionChange = false;
        }

        StatusMessage = $"当前项目：{project.Name}";
    }

    private async Task<ProjectId?> EnsureProjectAsync()
    {
        if (_currentProjectId is not null)
        {
            return _currentProjectId;
        }

        var projectName = Path.GetFileName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = "CutLab Project";
        }

        var archiveRoot = Path.GetDirectoryName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            ?? SourcePath;

        var result = await _createProjectHandler.HandleAsync(new CreateProjectCommand(
            projectName,
            Episode: 1,
            NamingTemplate: "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}",
            ArchivePathPattern: "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}",
            ArchiveFolders: ["分镜", "原画", "动画", "背景", "渲染"],
            RootPath: archiveRoot));

        return result.IsSuccess ? result.Value : null;
    }

    private bool _versionFilterApplied;

    private async Task LoadRenamePreviewAsync()
    {
        if (_currentSessionId is null)
        {
            return;
        }

        var preview = await _getScanPreviewHandler.HandleAsync(new GetScanPreviewQuery(
            _currentSessionId.Value,
            GetActiveVersionFilter()));
        if (preview.IsFailure || preview.Value is null)
        {
            StatusMessage = preview.Error ?? "预览加载失败。";
            return;
        }

        PreviewItems.Clear();
        foreach (var item in preview.Value.Items)
        {
            PreviewItems.Add(PreviewRowViewModel.FromInventory(item));
        }

        RebuildCutGallery();
        SelectedPreviewRow = PreviewItems.FirstOrDefault();
    }

    private void RebuildCutGallery()
    {
        CutGallery.Clear();
        var seenCutIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in PreviewItems.Where(item => item.IsImage && item.HasCutId))
        {
            if (seenCutIds.Add(row.CutId))
            {
                CutGallery.Add(new CutGalleryItemViewModel(row.CutId, row.FullPath));
            }
        }

        ShowGalleryHint = CutGallery.Count == 0 && PreviewItems.Count > 0;
        GalleryHint = ShowGalleryHint
            ? "下方缩略图仅展示已识别卡号。未识别文件请先在左侧列表查看，或通过插卡/重命名规范命名。"
            : string.Empty;
    }

    private string? GetActiveVersionFilter() =>
        _versionFilterApplied && !string.IsNullOrWhiteSpace(VersionTagFilter)
            ? VersionTagFilter.Trim()
            : null;

    private void UpdatePreviewFromArchive(IReadOnlyList<ArchivePlanItem> items)
    {
        PreviewItems.Clear();
        foreach (var item in items)
        {
            PreviewItems.Add(PreviewRowViewModel.FromArchive(item));
        }
    }

    private async Task LoadMissingCutsAsync(bool showFailureAsStatus = false)
    {
        if (_currentProjectId is null || _currentSessionId is null)
        {
            MissingCutsSummary = string.Empty;
            return;
        }

        var result = await _getMissingCutsFromSessionHandler.HandleAsync(
            new GetMissingCutsFromSessionQuery(
                _currentProjectId.Value,
                _currentSessionId.Value,
                ResolveMissingCutScope(MissingCutFrom),
                ResolveMissingCutScope(MissingCutTo)));

        if (result.IsFailure || result.Value is null)
        {
            MissingCutsSummary = showFailureAsStatus
                ? result.Error ?? "缺卡检测失败。"
                : string.Empty;
            return;
        }

        if (result.Value.MissingCuts.Count == 0 && result.Value.MissingInsertSuffixes.Count == 0)
        {
            MissingCutsSummary = $"C{result.Value.Scope.From.Cut:D3}-C{result.Value.Scope.To.Cut:D3} 范围内无缺卡。";
            return;
        }

        var parts = new List<string>();
        if (result.Value.MissingCuts.Count > 0)
        {
            var missingNumbers = string.Join(", ", result.Value.MissingCuts.Select(m => $"C{m.CutNumber.Cut:D3}"));
            parts.Add($"缺卡 {result.Value.MissingCuts.Count} 个：{missingNumbers}");
        }

        if (result.Value.MissingInsertSuffixes.Count > 0)
        {
            var missingInserts = string.Join(", ", result.Value.MissingInsertSuffixes.Select(
                missing => $"C{missing.CutNumber.Cut:D3}{missing.MissingSuffix}"));
            parts.Add($"缺插卡后缀 {result.Value.MissingInsertSuffixes.Count} 个：{missingInserts}");
        }

        MissingCutsSummary = string.Join("；", parts);
    }

    private static int? ResolveMissingCutScope(int value) => value > 0 ? value : null;

    private bool CanPickFolder() => !IsBusy;

    private bool CanScan() => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath);

    private bool CanOpenSettings() => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath);

    private bool CanManageProjectConfig() => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath);

    private bool CanImportProjectConfig() => !IsBusy;

    private bool CanExport() => !IsBusy && _currentSessionId is not null;

    private bool CanExecuteRename() => !IsBusy && _currentSessionId is not null;

    private bool CanExecuteArchive() => !IsBusy && _currentSessionId is not null;

    private bool CanDetectMissingCuts() => !IsBusy && _currentSessionId is not null;

    private bool CanExportMissingCuts() => !IsBusy && _currentSessionId is not null;

    private bool CanDeleteProject() => !IsBusy && _currentProjectId is not null;

    private bool CanUndo() => !IsBusy && _currentProjectId is not null;

    private bool CanInsertCut() => !IsBusy && _currentSessionId is not null && InsertAfterCut > 0;

    private bool CanBatchAutoNumber() => !IsBusy && _currentSessionId is not null && InsertAfterCut > 0;

    private bool CanGeneratePreview() => !IsBusy && _currentSessionId is not null;

    private bool CanApplyVersionFilter() => !IsBusy && _currentSessionId is not null;

    private bool CanManageProjects() => !IsBusy;

    private void NotifyCommandsChanged()
    {
        PickFolderCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ExportCutListCommand.NotifyCanExecuteChanged();
        ExportProgressReportCommand.NotifyCanExecuteChanged();
        ExportProjectConfigCommand.NotifyCanExecuteChanged();
        ImportProjectConfigCommand.NotifyCanExecuteChanged();
        ExecuteRenameCommand.NotifyCanExecuteChanged();
        CreateArchiveDirectoriesCommand.NotifyCanExecuteChanged();
        MoveToArchiveCommand.NotifyCanExecuteChanged();
        DetectMissingCutsCommand.NotifyCanExecuteChanged();
        ExportMissingCutsCommand.NotifyCanExecuteChanged();
        DeleteProjectCommand.NotifyCanExecuteChanged();
        InsertCutCommand.NotifyCanExecuteChanged();
        BatchAutoNumberCommand.NotifyCanExecuteChanged();
        GeneratePreviewVideoCommand.NotifyCanExecuteChanged();
        ApplyVersionFilterCommand.NotifyCanExecuteChanged();
        RefreshProjectsCommand.NotifyCanExecuteChanged();
        NewProjectCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProjectChanged(ProjectListItemViewModel? value)
    {
        if (_suppressProjectSelectionChange || value is null || value.Id == _currentProjectId)
        {
            return;
        }

        _ = ApplyProjectSwitchAsync(value);
    }

    partial void OnSelectedPreviewRowChanged(PreviewRowViewModel? value)
    {
        PreviewImage?.Dispose();
        PreviewImage = null;

        if (value is null)
        {
            PreviewCaption = "选择左侧列表中的文件以预览。";
            return;
        }

        if (!value.HasCutId)
        {
            PreviewCaption = $"未识别 · {value.Source}（{value.Message}）";
        }
        else
        {
            PreviewCaption = $"{value.CutId} · {value.Source}";
        }

        if (!value.IsImage || !File.Exists(value.FullPath))
        {
            PreviewCaption = $"{PreviewCaption}（非图片文件）";
            return;
        }

        try
        {
            PreviewImage = new Bitmap(value.FullPath);
        }
        catch
        {
            PreviewCaption = $"{PreviewCaption}（无法加载预览）";
        }
    }

    partial void OnSourcePathChanged(string value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ExportProjectConfigCommand.NotifyCanExecuteChanged();
        ImportProjectConfigCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandsChanged();

    partial void OnMissingCutFromChanged(int value) => DetectMissingCutsCommand.NotifyCanExecuteChanged();

    partial void OnMissingCutToChanged(int value) => DetectMissingCutsCommand.NotifyCanExecuteChanged();

    partial void OnInsertAfterCutChanged(int value) => InsertCutCommand.NotifyCanExecuteChanged();
}

public sealed class PreviewRowViewModel
{
    public PreviewRowViewModel(
        string cutId,
        CutNumber? parsedCut,
        string fullPath,
        string source,
        string target,
        string status,
        string message)
    {
        CutId = cutId;
        ParsedCut = parsedCut;
        FullPath = fullPath;
        Source = source;
        Target = target;
        Status = status;
        Message = message;
        IsImage = ImagePreviewSupport.IsImageFile(fullPath);
    }

    public string CutId { get; }

    public CutNumber? ParsedCut { get; }

    public int? ReorderCut => ParsedCut?.Cut;

    public string DisplayCutId => HasCutId ? CutId : "（未识别）";

    public bool HasCutId => !string.IsNullOrWhiteSpace(CutId);

    public string FullPath { get; }

    public bool IsImage { get; }

    public string Source { get; }

    public string Target { get; }

    public string Status { get; }

    public string Message { get; }

    public static PreviewRowViewModel FromInventory(ScanInventoryItem item) =>
        new(
            item.CutId,
            item.ParsedCut,
            item.SourcePath.Value,
            Path.GetFileName(item.SourcePath.Value),
            item.TargetDisplay,
            item.Status,
            item.Message);

    public static PreviewRowViewModel FromRename(RenamePlanItem item) =>
        new(
            string.Empty,
            null,
            item.SourcePath.Value,
            Path.GetFileName(item.SourcePath.Value),
            item.ProposedFileName.Value,
            item.Status.ToString(),
            item.Message ?? string.Empty);

    public static PreviewRowViewModel FromArchive(ArchivePlanItem item) =>
        new(
            string.Empty,
            null,
            item.SourcePath?.Value ?? string.Empty,
            item.SourcePath is null ? "(建目录)" : Path.GetFileName(item.SourcePath.Value.Value),
            item.TargetPath.Value,
            item.Status.ToString(),
            item.Message ?? string.Empty);
}
