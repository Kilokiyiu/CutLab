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
using CutLab.Application.Operations.ExecuteArchive;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Operations.UndoLastOperation;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.ListRecentProjects;
using CutLab.Application.Reporting.ExportCutList;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.GetScanPreview;
using CutLab.Application.Scanning.ScanFolder;
using CutLab.Domain.Projects;

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

    private ProjectId? _currentProjectId;
    private Guid? _currentSessionId;

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
        UndoLastOperationHandler undoLastOperationHandler)
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
        PreviewItems = [];
        _ = InitializeAsync();
    }

    public string Title { get; } = "CutLab";

    public string Subtitle { get; } = "重命名 · 归档 · 缺卡检测 · 导出";

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "请选择文件夹并扫描。";

    [ObservableProperty]
    private string _missingCutsSummary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _scanRecursive;

    public ObservableCollection<PreviewRowViewModel> PreviewItems { get; }

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

            await LoadRenamePreviewAsync();
            await LoadMissingCutsAsync();

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

            var result = await _exportCutListHandler.HandleAsync(new ExportCutListCommand(
                _currentProjectId.Value,
                _currentSessionId.Value,
                outputPath));

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
        var projects = await _listRecentProjectsHandler.HandleAsync(new ListRecentProjectsQuery(1));
        if (projects.Count > 0)
        {
            _currentProjectId = projects[0].Id;
            SourcePath = projects[0].RootPath;
            StatusMessage = $"已加载项目：{projects[0].Name}";
        }
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

    private async Task LoadRenamePreviewAsync()
    {
        if (_currentSessionId is null)
        {
            return;
        }

        var preview = await _getScanPreviewHandler.HandleAsync(new GetScanPreviewQuery(_currentSessionId.Value));
        if (preview.IsFailure || preview.Value is null)
        {
            StatusMessage = preview.Error ?? "预览加载失败。";
            return;
        }

        PreviewItems.Clear();
        foreach (var item in preview.Value.Items)
        {
            PreviewItems.Add(PreviewRowViewModel.FromRename(item));
        }
    }

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
            new GetMissingCutsFromSessionQuery(_currentProjectId.Value, _currentSessionId.Value));

        if (result.IsFailure || result.Value is null)
        {
            MissingCutsSummary = showFailureAsStatus
                ? result.Error ?? "缺卡检测失败。"
                : string.Empty;
            return;
        }

        if (result.Value.MissingCuts.Count == 0)
        {
            MissingCutsSummary = $"C{result.Value.Scope.From.Cut:D3}-C{result.Value.Scope.To.Cut:D3} 范围内无缺卡。";
            return;
        }

        var missingNumbers = string.Join(", ", result.Value.MissingCuts.Select(m => $"C{m.CutNumber.Cut:D3}"));
        MissingCutsSummary =
            $"缺卡 {result.Value.MissingCuts.Count} 个（共 {result.Value.TotalExpected} 卡）：{missingNumbers}";
    }

    private bool CanPickFolder() => !IsBusy;

    private bool CanScan() => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath);

    private bool CanOpenSettings() => !IsBusy && !string.IsNullOrWhiteSpace(SourcePath);

    private bool CanExport() => !IsBusy && _currentSessionId is not null;

    private bool CanExecuteRename() => !IsBusy && _currentSessionId is not null;

    private bool CanExecuteArchive() => !IsBusy && _currentSessionId is not null;

    private bool CanDetectMissingCuts() => !IsBusy && _currentSessionId is not null;

    private bool CanUndo() => !IsBusy && _currentProjectId is not null;

    private void NotifyCommandsChanged()
    {
        PickFolderCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ExportCutListCommand.NotifyCanExecuteChanged();
        ExecuteRenameCommand.NotifyCanExecuteChanged();
        CreateArchiveDirectoriesCommand.NotifyCanExecuteChanged();
        MoveToArchiveCommand.NotifyCanExecuteChanged();
        DetectMissingCutsCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    partial void OnSourcePathChanged(string value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandsChanged();
}

public sealed class PreviewRowViewModel
{
    public PreviewRowViewModel(string source, string target, string status, string message)
    {
        Source = source;
        Target = target;
        Status = status;
        Message = message;
    }

    public string Source { get; }

    public string Target { get; }

    public string Status { get; }

    public string Message { get; }

    public static PreviewRowViewModel FromRename(RenamePlanItem item) =>
        new(
            Path.GetFileName(item.SourcePath.Value),
            item.ProposedFileName.Value,
            item.Status.ToString(),
            item.Message ?? string.Empty);

    public static PreviewRowViewModel FromArchive(ArchivePlanItem item) =>
        new(
            item.SourcePath is null ? "(建目录)" : Path.GetFileName(item.SourcePath.Value.Value),
            item.TargetPath.Value,
            item.Status.ToString(),
            item.Message ?? string.Empty);
}
