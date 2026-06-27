namespace CutLab.App.ViewModels;

using CutLab.Domain.Projects;

public sealed class ProjectListItemViewModel
{
    public ProjectListItemViewModel(ProjectId id, string name, string rootPath, int episode)
    {
        Id = id;
        Name = name;
        RootPath = rootPath;
        Episode = episode;
    }

    public ProjectId Id { get; }

    public string Name { get; }

    public string RootPath { get; }

    public int Episode { get; }

    public string DisplayName => $"{Name} · EP{Episode:D2}";

    public override string ToString() => DisplayName;
}
