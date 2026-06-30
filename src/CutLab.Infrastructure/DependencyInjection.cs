namespace CutLab.Infrastructure;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Cuts;
using CutLab.Domain.Operations;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Infrastructure.Archive;
using CutLab.Infrastructure.Export;
using CutLab.Infrastructure.FileSystem;
using CutLab.Infrastructure.Naming;
using CutLab.Infrastructure.Persistence;
using CutLab.Infrastructure.Recognition;
using CutLab.Infrastructure.Video;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? dataDirectory = null)
    {
        var storeDirectory = dataDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CutLab",
                "projects");

        services.AddSingleton<IAnimationProjectRepository>(_ => new JsonProjectRepository(storeDirectory));
        services.AddSingleton<ICutRegistryRepository, InMemoryCutRegistryRepository>();
        services.AddSingleton<IScanSessionRepository, InMemoryScanSessionRepository>();
        services.AddSingleton<IOperationBatchRepository, InMemoryOperationBatchRepository>();
        services.AddSingleton<IUnitOfWork, JsonUnitOfWork>();
        services.AddSingleton<IFileSystemGateway, LocalFileSystemGateway>();
        services.AddSingleton<INamingService, TemplateNamingService>();
        services.AddSingleton<IRecognitionService, RegexRecognitionService>();
        services.AddSingleton<IFrameSequenceAnalyzer, FrameSequenceAnalyzer>();
        services.AddSingleton<IFrameSequenceFileParser, FrameSequenceFileParser>();
        services.AddSingleton<IArchivePathResolver, TemplateArchivePathResolver>();
        services.AddSingleton<ICutListExportService, MiniExcelCutListExportService>();
        services.AddSingleton<IProgressReportExportService, MiniExcelProgressReportExportService>();
        services.AddSingleton<IPreviewVideoGenerator, FfmpegPreviewVideoGenerator>();
        return services;
    }
}
