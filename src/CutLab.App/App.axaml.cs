using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CutLab.App.Services;
using CutLab.App.ViewModels;
using CutLab.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CutLab.App;

public partial class App : global::Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = Program.Services.GetRequiredService<MainWindowViewModel>(),
            };

            Program.Services.GetRequiredService<IFileDialogService>().SetOwner(mainWindow);
            Program.Services.GetRequiredService<IWindowService>().SetOwner(mainWindow);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
