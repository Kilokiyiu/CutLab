namespace CutLab.Application;

using CutLab.Application.Operations.BatchAutoNumberUnrecognized;
using CutLab.Application.Operations.InsertCut;
using CutLab.Application.Operations.ReorderCuts;
using CutLab.Application.Operations.ExecuteArchive;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Operations.UndoLastOperation;
using CutLab.Application.Operations.ListOperationHistory;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.ListRecentProjects;
using CutLab.Application.Projects.GetProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Application.Projects.DeleteProject;
using CutLab.Application.Projects.ProjectConfig;
using CutLab.Application.Projects.Templates;
using CutLab.Application.Reporting.AnalyzeFrameSequences;
using CutLab.Application.Reporting.ExportFrameSequenceIssues;
using CutLab.Application.Reporting.ExportMissingCuts;
using CutLab.Application.Reporting.ExportCutList;
using CutLab.Application.Reporting.ExportProgressReport;
using CutLab.Application.Reporting.GeneratePreviewVideo;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.GetScanPreview;
using CutLab.Application.Scanning.ScanFolder;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        string? templatesDirectory = null)
    {
        var templateDirectory = templatesDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "templates");
        services.AddSingleton<IProjectTemplateCatalog>(_ => new ProjectTemplateCatalog(templateDirectory));
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ListRecentProjectsHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<UpdateProjectSettingsHandler>();
        services.AddScoped<ExportProjectConfigHandler>();
        services.AddScoped<ImportProjectConfigHandler>();
        services.AddScoped<DeleteProjectHandler>();
        services.AddScoped<ScanFolderHandler>();
        services.AddScoped<GetScanPreviewHandler>();
        services.AddScoped<ExecuteRenameHandler>();
        services.AddScoped<InsertCutHandler>();
        services.AddScoped<BatchAutoNumberUnrecognizedHandler>();
        services.AddScoped<ReorderCutsHandler>();
        services.AddScoped<ExecuteArchiveHandler>();
        services.AddScoped<UndoLastOperationHandler>();
        services.AddScoped<ListOperationHistoryHandler>();
        services.AddScoped<GetMissingCutsFromSessionHandler>();
        services.AddScoped<ExportMissingCutsHandler>();
        services.AddScoped<AnalyzeFrameSequencesFromSessionHandler>();
        services.AddScoped<ExportFrameSequenceIssuesHandler>();
        services.AddScoped<ExportCutListHandler>();
        services.AddScoped<ExportProgressReportHandler>();
        services.AddScoped<GeneratePreviewVideoHandler>();
        return services;
    }
}
