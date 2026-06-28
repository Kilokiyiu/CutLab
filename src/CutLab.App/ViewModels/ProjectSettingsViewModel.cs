namespace CutLab.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutLab.Application.Projects.GetProject;
using CutLab.Application.Projects.Templates;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Domain.Projects;

public partial class ProjectSettingsViewModel : ViewModelBase
{
    private readonly GetProjectHandler _getProjectHandler;
    private readonly UpdateProjectSettingsHandler _updateProjectSettingsHandler;
    private readonly IProjectTemplateCatalog _templateCatalog;
    private ProjectId _projectId;

    public ProjectSettingsViewModel(
        GetProjectHandler getProjectHandler,
        UpdateProjectSettingsHandler updateProjectSettingsHandler,
        IProjectTemplateCatalog templateCatalog)
    {
        _getProjectHandler = getProjectHandler;
        _updateProjectSettingsHandler = updateProjectSettingsHandler;
        _templateCatalog = templateCatalog;
        _projectId = ProjectId.New();
        AvailableTemplates = _templateCatalog.List();
    }

    public IReadOnlyList<ProjectTemplateSummary> AvailableTemplates { get; }

    [ObservableProperty]
    private ProjectTemplateSummary? _selectedTemplate;

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
    private string _recognitionPatternsText = string.Empty;

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
        RecognitionPatternsText = result.Value.RecognitionPatternsText;
        ErrorMessage = string.Empty;
        return true;
    }

    [RelayCommand]
    private void LoadSelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            ErrorMessage = "请先选择模板。";
            return;
        }

        var template = _templateCatalog.Get(SelectedTemplate.Name);
        if (template is null)
        {
            ErrorMessage = "无法加载所选模板。";
            return;
        }

        NamingTemplate = template.NamingTemplate;
        ArchivePathPattern = template.ArchivePathPattern;
        ArchiveFoldersText = string.Join(", ", template.ArchiveFolders);
        RecognitionPatternsText = string.Join(Environment.NewLine, template.RecognitionPatterns);
        ErrorMessage = string.Empty;
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
            DefaultVersionTag,
            RecognitionPatternsText));

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
