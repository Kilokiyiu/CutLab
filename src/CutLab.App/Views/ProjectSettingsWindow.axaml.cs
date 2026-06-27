using Avalonia.Controls;
using Avalonia.Interactivity;
using CutLab.App.ViewModels;

namespace CutLab.App.Views;

public partial class ProjectSettingsWindow : Window
{
    public bool Saved { get; private set; }

    public ProjectSettingsWindow()
    {
        InitializeComponent();
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectSettingsViewModel viewModel)
        {
            Saved = await viewModel.TrySaveAsync();
            if (Saved)
            {
                Close();
            }
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Saved = false;
        Close();
    }
}
