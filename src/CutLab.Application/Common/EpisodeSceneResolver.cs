namespace CutLab.Application.Common;

using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using System.Text.RegularExpressions;

public static partial class EpisodeSceneResolver
{
    public static (int Episode, int Scene) Resolve(ScanSession session, AnimationProject project)
    {
        var recognized = session.GetRecognized()
            .FirstOrDefault(asset => asset.ParsedCut is not null);

        if (recognized?.ParsedCut is { } cut)
        {
            return (cut.Episode, cut.Scene);
        }

        var sceneMatch = SceneTokenPattern().Match(project.NamingConvention.Template);
        var scene = sceneMatch.Success ? int.Parse(sceneMatch.Groups["value"].Value) : 1;
        return (project.Episode.Value, scene);
    }

    [GeneratedRegex(@"S(?<value>\d+)")]
    private static partial Regex SceneTokenPattern();
}
