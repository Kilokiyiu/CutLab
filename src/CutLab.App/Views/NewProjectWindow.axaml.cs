using Avalonia.Controls;
using Avalonia.Interactivity;
using CutLab.App.Services;
using CutLab.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CutLab.App.Views;

public partial class NewProjectWindow : Window
{
    public bool Created { get; private set; }

    public NewProjectViewModel ViewModel { get; }

    public NewProjectWindow()
    {
        ViewModel = Program.Services.GetRequiredService<NewProjectViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialogService = Program.Services.GetRequiredService<IFileDialogService>();
        var folder = await dialogService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ViewModel.SourcePath = folder;
        }
    }

    private void CreateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryBuildResult())
        {
            return;
        }

        Created = true;
        Close();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Created = false;
        Close();
    }
}
