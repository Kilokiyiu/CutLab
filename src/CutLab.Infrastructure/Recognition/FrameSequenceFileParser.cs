namespace CutLab.Infrastructure.Recognition;

using System.Text.RegularExpressions;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed partial class FrameSequenceFileParser : IFrameSequenceFileParser
{
    public ParsedFrameFile? TryParse(
        string filePath,
        FrameSequenceSettings settings,
        NamingConvention namingConvention)
    {
        if (!settings.Enabled)
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var regex = RecognitionPatternCompiler.TryCompileFramePattern(
            settings.FileNamePattern,
            namingConvention.TypeSuffixes);
        if (regex?.Match(fileName) is not { Success: true } match)
        {
            return null;
        }

        if (!match.Groups["frame"].Success || !int.TryParse(match.Groups["frame"].Value, out var frameNumber))
        {
            return null;
        }

        CutNumber? cutNumber = null;
        if (match.Groups["cut"].Success && int.TryParse(match.Groups["cut"].Value, out var cut))
        {
            var episode = match.Groups["ep"].Success
                ? int.Parse(match.Groups["ep"].Value)
                : ExtractEpisode(namingConvention);
            var scene = match.Groups["sc"].Success
                ? int.Parse(match.Groups["sc"].Value)
                : ExtractScene(namingConvention);
            var insert = match.Groups["insert"].Success && !string.IsNullOrEmpty(match.Groups["insert"].Value)
                ? match.Groups["insert"].Value
                : null;
            cutNumber = new CutNumber(episode, scene, cut, insert);
        }

        return new ParsedFrameFile(new FilePath(filePath), cutNumber, frameNumber);
    }

    private static int ExtractEpisode(NamingConvention convention)
    {
        var match = EpisodeTokenPattern().Match(convention.Template);
        return match.Success ? int.Parse(match.Groups["value"].Value) : 1;
    }

    private static int ExtractScene(NamingConvention convention)
    {
        var match = SceneTokenPattern().Match(convention.Template);
        return match.Success ? int.Parse(match.Groups["value"].Value) : 1;
    }

    [GeneratedRegex(@"EP(?<value>\d+)")]
    private static partial Regex EpisodeTokenPattern();

    [GeneratedRegex(@"S(?<value>\d+)")]
    private static partial Regex SceneTokenPattern();
}
