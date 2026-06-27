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
}
