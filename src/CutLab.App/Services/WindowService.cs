namespace CutLab.App.Services;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CutLab.App.ViewModels;
using CutLab.App.Views;
using CutLab.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

public interface IWindowService
{
    void SetOwner(object ownerWindow);

    Task<bool> ShowProjectSettingsAsync(ProjectId projectId);

    Task<NewProjectDialogResult?> ShowNewProjectAsync();

    Task<bool> ConfirmAsync(string title, string message);
}

public sealed class WindowService : IWindowService
{
    private Window? _owner;

    public void SetOwner(object ownerWindow)
    {
        _owner = ownerWindow as Window;
    }

    public async Task<bool> ShowProjectSettingsAsync(ProjectId projectId)
    {
        if (_owner is null)
        {
            return false;
        }

        var viewModel = Program.Services.GetRequiredService<ProjectSettingsViewModel>();
        if (!await viewModel.InitializeAsync(projectId))
        {
            return false;
        }

        var window = new ProjectSettingsWindow
        {
            DataContext = viewModel
        };

        await window.ShowDialog(_owner);
        return window.Saved;
    }

    public async Task<NewProjectDialogResult?> ShowNewProjectAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        var window = new NewProjectWindow();
        await window.ShowDialog(_owner);
        return window.Created ? window.ViewModel.Result : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_owner is null)
        {
            return false;
        }

        Window? dialog = null;
        dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = BuildConfirmContent(message, confirmed => dialog!.Close(confirmed))
        };

        var result = await dialog.ShowDialog<bool?>(_owner);
        return result == true;
    }

    private static Control BuildConfirmContent(string message, Action<bool> close)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button { Content = "取消" };
        cancelButton.Click += (_, _) => close(false);
        buttons.Children.Add(cancelButton);

        var okButton = new Button { Content = "确定", Classes = { "primary" } };
        okButton.Click += (_, _) => close(true);
        buttons.Children.Add(okButton);

        panel.Children.Add(buttons);
        return panel;
    }
}
