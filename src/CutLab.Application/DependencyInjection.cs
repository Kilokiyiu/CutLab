namespace CutLab.Application;

using CutLab.Application.Projects.CreateProject;
using CutLab.Application.Reporting.GetMissingCuts;
using CutLab.Application.Scanning.ScanFolder;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ScanFolderHandler>();
        services.AddScoped<GetMissingCutsHandler>();
        return services;
    }
}
