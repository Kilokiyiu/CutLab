namespace CutLab.App.Services;

using Avalonia.Controls;
using CutLab.App.ViewModels;
using CutLab.App.Views;
using CutLab.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

public interface IWindowService
{
    void SetOwner(object ownerWindow);

    Task<bool> ShowProjectSettingsAsync(ProjectId projectId);
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
}
