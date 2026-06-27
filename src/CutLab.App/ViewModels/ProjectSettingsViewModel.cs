namespace CutLab.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutLab.Application.Projects.GetProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Domain.Projects;

public partial class ProjectSettingsViewModel : ViewModelBase
{
    private readonly GetProjectHandler _getProjectHandler;
    private readonly UpdateProjectSettingsHandler _updateProjectSettingsHandler;
    private ProjectId _projectId;

    public ProjectSettingsViewModel(
        GetProjectHandler getProjectHandler,
        UpdateProjectSettingsHandler updateProjectSettingsHandler)
    {
        _getProjectHandler = getProjectHandler;
        _updateProjectSettingsHandler = updateProjectSettingsHandler;
        _projectId = ProjectId.New();
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _episode = 1;

    [ObservableProperty]
    private string _namingTemplate = "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}";

    [ObservableProperty]
    private string _archivePathPattern = "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}";

    [ObservableProperty]
    private string _archiveFoldersText = "分镜, 原画, 动画, 背景, 渲染";

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private string _defaultVersionTag = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public async Task<bool> InitializeAsync(ProjectId projectId)
    {
        _projectId = projectId;
        var result = await _getProjectHandler.HandleAsync(new GetProjectQuery(projectId));
        if (result.IsFailure || result.Value is null)
        {
            ErrorMessage = result.Error ?? "加载项目失败。";
            return false;
        }

        Name = result.Value.Name;
        Episode = result.Value.Episode;
        NamingTemplate = result.Value.NamingTemplate;
        ArchivePathPattern = result.Value.ArchivePathPattern;
        ArchiveFoldersText = result.Value.ArchiveFoldersText;
        RootPath = result.Value.RootPath;
        DefaultVersionTag = result.Value.DefaultVersionTag;
        ErrorMessage = string.Empty;
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        var result = await _updateProjectSettingsHandler.HandleAsync(new UpdateProjectSettingsCommand(
            _projectId,
            Name,
            Episode,
            NamingTemplate,
            ArchivePathPattern,
            ArchiveFoldersText,
            RootPath,
            DefaultVersionTag));

        if (result.IsFailure)
        {
            ErrorMessage = result.Error ?? "保存失败。";
        }
    }

    public async Task<bool> TrySaveAsync()
    {
        await SaveAsync();
        return string.IsNullOrEmpty(ErrorMessage);
    }
}
