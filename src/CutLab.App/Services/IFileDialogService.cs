namespace CutLab.App.Services;

public interface IFileDialogService
{
    void SetOwner(object ownerWindow);

    Task<string?> PickFolderAsync();

    Task<string?> SaveExcelFileAsync(string suggestedFileName);

    Task<string?> SaveVideoFileAsync(string suggestedFileName);
}
