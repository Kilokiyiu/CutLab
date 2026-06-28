namespace CutLab.Infrastructure.Recognition;

using System.Text.RegularExpressions;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed partial class RegexRecognitionService : IRecognitionService
{
    public RecognitionResult TryParse(
        string fileName,
        IReadOnlyList<RecognitionPattern> patterns,
        NamingConvention defaultConvention)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        if (StandardCutPattern().Match(nameWithoutExtension) is { Success: true } standardMatch)
        {
            return ParseStandardMatch(standardMatch);
        }

        if (SimpleCutPattern().Match(nameWithoutExtension) is { Success: true } simpleMatch)
        {
            return ParseSimpleMatch(simpleMatch, defaultConvention);
        }

        if (ChineseCardPattern().Match(nameWithoutExtension) is { Success: true } chineseMatch)
        {
            return ParseChineseMatch(chineseMatch, defaultConvention);
        }

        if (OrdinalCardPattern().Match(nameWithoutExtension) is { Success: true } ordinalMatch)
        {
            return ParseOrdinalMatch(ordinalMatch);
        }

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Pattern))
            {
                continue;
            }

            var regex = RecognitionPatternCompiler.TryCompile(pattern.Pattern);
            if (regex?.Match(nameWithoutExtension) is not { Success: true } customMatch)
            {
                continue;
            }

            return ParseCustomMatch(customMatch, defaultConvention);
        }

        return new RecognitionResult(null, null, RecognitionStatus.Unrecognized, "未匹配任何识别规则。");
    }

    private static RecognitionResult ParseStandardMatch(Match match)
    {
        var episode = int.Parse(match.Groups["ep"].Value);
        var scene = int.Parse(match.Groups["sc"].Value);
        var cut = int.Parse(match.Groups["cut"].Value);
        var insert = string.IsNullOrEmpty(match.Groups["insert"].Value)
            ? null
            : match.Groups["insert"].Value;
        var typeText = match.Groups["type"].Value;
        var versionTag = ParseVersionGroup(match);
        return BuildResult(episode, scene, cut, insert, typeText, versionTag);
    }

    private static RecognitionResult ParseSimpleMatch(Match match, NamingConvention defaultConvention)
    {
        var cut = int.Parse(match.Groups["cut"].Value);
        var insert = string.IsNullOrEmpty(match.Groups["insert"].Value)
            ? null
            : match.Groups["insert"].Value;
        var typeText = match.Groups["type"].Value;
        var versionTag = ParseVersionGroup(match);
        return BuildResult(defaultConvention, cut, insert, typeText, versionTag);
    }

    private static RecognitionResult ParseChineseMatch(Match match, NamingConvention defaultConvention)
    {
        var cut = int.Parse(match.Groups["cut"].Value);
        var typeText = match.Groups["type"].Value;
        return BuildResult(defaultConvention, cut, null, typeText, null);
    }

    private static RecognitionResult ParseOrdinalMatch(Match match)
    {
        var cut = int.Parse(match.Groups["cut"].Value);
        return new RecognitionResult(
            new CutNumber(1, 1, cut),
            null,
            RecognitionStatus.Unrecognized,
            "已识别卡号，但缺少资产类型。",
            null);
    }

    private static RecognitionResult ParseCustomMatch(Match match, NamingConvention defaultConvention)
    {
        if (!match.Groups["cut"].Success)
        {
            return new RecognitionResult(null, null, RecognitionStatus.Unrecognized, "自定义规则未识别卡号。");
        }

        var episode = match.Groups["ep"].Success
            ? int.Parse(match.Groups["ep"].Value)
            : ExtractEpisode(defaultConvention);
        var scene = match.Groups["sc"].Success
            ? int.Parse(match.Groups["sc"].Value)
            : ExtractScene(defaultConvention);
        var cut = int.Parse(match.Groups["cut"].Value);
        var insert = match.Groups["insert"].Success && !string.IsNullOrEmpty(match.Groups["insert"].Value)
            ? match.Groups["insert"].Value
            : null;
        var versionTag = ParseVersionGroup(match);

        if (!match.Groups["type"].Success)
        {
            return new RecognitionResult(
                new CutNumber(episode, scene, cut, insert),
                null,
                RecognitionStatus.Unrecognized,
                "已识别卡号，但缺少资产类型。",
                versionTag);
        }

        return BuildResult(episode, scene, cut, insert, match.Groups["type"].Value, versionTag);
    }

    private static VersionTag? ParseVersionGroup(Match match) =>
        match.Groups["version"].Success
            ? VersionTagParser.TryParse(match.Groups["version"].Value)
            : null;

    private static RecognitionResult BuildResult(
        NamingConvention convention,
        int cut,
        string? insertSuffix,
        string typeText,
        VersionTag? versionTag)
    {
        var episode = ExtractEpisode(convention);
        var scene = ExtractScene(convention);
        return BuildResult(episode, scene, cut, insertSuffix, typeText, versionTag);
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

    private static RecognitionResult BuildResult(
        int episode,
        int scene,
        int cut,
        string? insertSuffix,
        string typeText,
        VersionTag? versionTag)
    {
        var assetType = MapAssetType(typeText);
        if (assetType is null)
        {
            return new RecognitionResult(
                new CutNumber(episode, scene, cut, insertSuffix),
                null,
                RecognitionStatus.Unrecognized,
                $"无法识别资产类型：{typeText}",
                versionTag);
        }

        return new RecognitionResult(
            new CutNumber(episode, scene, cut, insertSuffix),
            assetType,
            RecognitionStatus.Recognized,
            null,
            versionTag);
    }

    private static AssetType? MapAssetType(string typeText) =>
        typeText switch
        {
            "分镜" => AssetType.Storyboard,
            "原画" => AssetType.Keyframe,
            "动画" => AssetType.Inbetween,
            "背景" => AssetType.Background,
            "渲染" => AssetType.Render,
            _ => null
        };

    [GeneratedRegex(@"^EP(?<ep>\d+)_S(?<sc>\d+)_C(?<cut>\d+)(?<insert>[a-z]?)_(?<type>分镜|原画|动画|背景|渲染)(?:_(?<version>v\d+|draft|s))?$", RegexOptions.IgnoreCase)]
    private static partial Regex StandardCutPattern();

    [GeneratedRegex(@"^C(?<cut>\d+)(?<insert>[a-z]?)_(?<type>分镜|原画|动画|背景|渲染)(?:_(?<version>v\d+|draft|s))?$", RegexOptions.IgnoreCase)]
    private static partial Regex SimpleCutPattern();

    [GeneratedRegex(@"^(?<cut>\d+)卡(?<type>分镜|原画|动画|背景|渲染)$")]
    private static partial Regex ChineseCardPattern();

    [GeneratedRegex(@"^第(?<cut>\d+)卡$")]
    private static partial Regex OrdinalCardPattern();

    [GeneratedRegex(@"EP(?<value>\d+)")]
    private static partial Regex EpisodeTokenPattern();

    [GeneratedRegex(@"S(?<value>\d+)")]
    private static partial Regex SceneTokenPattern();
}
