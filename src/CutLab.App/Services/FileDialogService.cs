namespace CutLab.App.Services;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

public sealed class FileDialogService : IFileDialogService
{
    private Window? _owner;

    public void SetOwner(object ownerWindow)
    {
        _owner = ownerWindow as Window;
    }

    public async Task<string?> PickFolderAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择待扫描文件夹",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> SaveExcelFileAsync(string suggestedFileName)
    {
        if (_owner is null)
        {
            return null;
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出卡号清单",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel 文件") { Patterns = ["*.xlsx"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> SaveVideoFileAsync(string suggestedFileName)
    {
        if (_owner is null)
        {
            return null;
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存预览视频",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "mp4",
            FileTypeChoices =
            [
                new FilePickerFileType("MP4 视频") { Patterns = ["*.mp4"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> SaveProjectConfigFileAsync(string suggestedFileName)
    {
        if (_owner is null)
        {
            return null;
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出项目配置",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "cutlab.json",
            FileTypeChoices =
            [
                new FilePickerFileType("CutLab 项目配置") { Patterns = ["*.cutlab.json", "*.json"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> OpenProjectConfigFileAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入项目配置",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CutLab 项目配置") { Patterns = ["*.cutlab.json", "*.json"] }
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> SaveMissingCutsFileAsync(string suggestedFileName)
    {
        if (_owner is null)
        {
            return null;
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出缺卡清单",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV 文件") { Patterns = ["*.csv"] },
                new FilePickerFileType("文本文件") { Patterns = ["*.txt"] }
            ]
        });

        return file?.Path.LocalPath;
    }
}
