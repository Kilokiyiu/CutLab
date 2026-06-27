using Avalonia;
using CutLab.App.Services;
using CutLab.App.ViewModels;
using CutLab.Application;
using CutLab.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CutLab.App;

sealed class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = ConfigureServices();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<global::CutLab.App.App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ProjectSettingsViewModel>();
        return services.BuildServiceProvider();
    }
}
