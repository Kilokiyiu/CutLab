namespace CutLab.Application;

using CutLab.Application.Operations.BatchAutoNumberUnrecognized;
using CutLab.Application.Operations.InsertCut;
using CutLab.Application.Operations.ExecuteArchive;
using CutLab.Application.Operations.ExecuteRename;
using CutLab.Application.Operations.UndoLastOperation;
using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Projects.ListRecentProjects;
using CutLab.Application.Projects.GetProject;
using CutLab.Application.Projects.UpdateProjectSettings;
using CutLab.Application.Reporting.ExportCutList;
using CutLab.Application.Reporting.ExportProgressReport;
using CutLab.Application.Reporting.GeneratePreviewVideo;
using CutLab.Application.Reporting.GetMissingCutsFromSession;
using CutLab.Application.Scanning.GetScanPreview;
using CutLab.Application.Scanning.ScanFolder;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ListRecentProjectsHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<UpdateProjectSettingsHandler>();
        services.AddScoped<ScanFolderHandler>();
        services.AddScoped<GetScanPreviewHandler>();
        services.AddScoped<ExecuteRenameHandler>();
        services.AddScoped<InsertCutHandler>();
        services.AddScoped<BatchAutoNumberUnrecognizedHandler>();
        services.AddScoped<ExecuteArchiveHandler>();
        services.AddScoped<UndoLastOperationHandler>();
        services.AddScoped<GetMissingCutsFromSessionHandler>();
        services.AddScoped<ExportCutListHandler>();
        services.AddScoped<ExportProgressReportHandler>();
        services.AddScoped<GeneratePreviewVideoHandler>();
        return services;
    }
}
