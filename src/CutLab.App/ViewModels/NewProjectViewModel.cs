namespace CutLab.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.Templates;
using CutLab.Domain.Projects;
using CutLab.Application.Common;

public sealed record NewProjectDialogResult(CreateProjectCommand Command, string SourcePath);

public partial class NewProjectViewModel : ViewModelBase
{
    private readonly IProjectTemplateCatalog _templateCatalog;

    public NewProjectViewModel(IProjectTemplateCatalog templateCatalog)
    {
        _templateCatalog = templateCatalog;
        AvailableTemplates = _templateCatalog.List();
        SelectedTemplate = AvailableTemplates.FirstOrDefault();
        ApplySelectedTemplate();
    }

    public IReadOnlyList<ProjectTemplateSummary> AvailableTemplates { get; }

    public NewProjectDialogResult? Result { get; private set; }

    [ObservableProperty]
    private ProjectTemplateSummary? _selectedTemplate;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _episode = 1;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private string _namingTemplate = "EP{EP:02}_S{SC:02}_C{CUT:03}_{TYPE}";

    [ObservableProperty]
    private string _archivePathPattern = "EP{EP:02}/S{SC:02}/C{CUT:03}/{TYPE}";

    [ObservableProperty]
    private string _archiveFoldersText = "分镜, 原画, 动画, 背景, 渲染";

    [ObservableProperty]
    private string _typeSuffixesText = "分镜, 原画, 动画, 背景, 渲染";

    [ObservableProperty]
    private string _recognitionPatternsText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    partial void OnSelectedTemplateChanged(ProjectTemplateSummary? value) => ApplySelectedTemplate();

    partial void OnSourcePathChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (string.IsNullOrWhiteSpace(RootPath))
        {
            RootPath = Path.GetDirectoryName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                ?? value;
        }
    }

    [RelayCommand]
    private void ApplySelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        var template = _templateCatalog.Get(SelectedTemplate.Name);
        if (template is null)
        {
            return;
        }

        NamingTemplate = template.NamingTemplate;
        ArchivePathPattern = template.ArchivePathPattern;
        ArchiveFoldersText = string.Join(", ", template.ArchiveFolders);
        TypeSuffixesText = TypeSuffixesTextFromTemplate(template);
        RecognitionPatternsText = string.Join(Environment.NewLine, template.RecognitionPatterns);
        ErrorMessage = string.Empty;
    }

    public bool TryBuildResult()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "项目名称不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            ErrorMessage = "请选择有效的素材文件夹。";
            return false;
        }

        var rootPath = string.IsNullOrWhiteSpace(RootPath) ? SourcePath : RootPath.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            ErrorMessage = "归档根路径不能为空。";
            return false;
        }

        var folders = ArchiveFoldersText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        Result = new NewProjectDialogResult(
            new CreateProjectCommand(
                Name.Trim(),
                Episode,
                NamingTemplate,
                ArchivePathPattern,
                folders,
                rootPath,
                RecognitionPatternsText,
                TypeSuffixesText),
            SourcePath.Trim());

        return true;
    }

    private static string TypeSuffixesTextFromTemplate(ProjectTemplateDefinition template) =>
        TypeSuffixesParser.Format(template.TypeSuffixes);
}
