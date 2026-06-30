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
        var suffixes = MergeTypeSuffixes(defaultConvention.TypeSuffixes);

        if (BuildStandardPattern(suffixes).Match(nameWithoutExtension) is { Success: true } standardMatch)
        {
            return ParseStandardMatch(standardMatch, suffixes);
        }

        if (BuildSimplePattern(suffixes).Match(nameWithoutExtension) is { Success: true } simpleMatch)
        {
            return ParseSimpleMatch(simpleMatch, defaultConvention, suffixes);
        }

        if (BuildChineseCardPattern(suffixes).Match(nameWithoutExtension) is { Success: true } chineseMatch)
        {
            return ParseChineseMatch(chineseMatch, defaultConvention, suffixes);
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

            var regex = RecognitionPatternCompiler.TryCompile(pattern.Pattern, suffixes);
            if (regex?.Match(nameWithoutExtension) is not { Success: true } customMatch)
            {
                continue;
            }

            return ParseCustomMatch(customMatch, defaultConvention, suffixes);
        }

        return new RecognitionResult(null, null, RecognitionStatus.Unrecognized, "未匹配任何识别规则。");
    }

    private static RecognitionResult ParseStandardMatch(Match match, IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var episode = int.Parse(match.Groups["ep"].Value);
        var scene = int.Parse(match.Groups["sc"].Value);
        var cut = int.Parse(match.Groups["cut"].Value);
        var insert = string.IsNullOrEmpty(match.Groups["insert"].Value)
            ? null
            : match.Groups["insert"].Value;
        var typeText = match.Groups["type"].Value;
        var versionTag = ParseVersionGroup(match);
        return BuildResult(episode, scene, cut, insert, typeText, versionTag, suffixes);
    }

    private static RecognitionResult ParseSimpleMatch(
        Match match,
        NamingConvention defaultConvention,
        IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var cut = int.Parse(match.Groups["cut"].Value);
        var insert = string.IsNullOrEmpty(match.Groups["insert"].Value)
            ? null
            : match.Groups["insert"].Value;
        var typeText = match.Groups["type"].Value;
        var versionTag = ParseVersionGroup(match);
        return BuildResult(defaultConvention, cut, insert, typeText, versionTag, suffixes);
    }

    private static RecognitionResult ParseChineseMatch(
        Match match,
        NamingConvention defaultConvention,
        IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var cut = int.Parse(match.Groups["cut"].Value);
        var typeText = match.Groups["type"].Value;
        return BuildResult(defaultConvention, cut, null, typeText, null, suffixes);
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

    private static RecognitionResult ParseCustomMatch(
        Match match,
        NamingConvention defaultConvention,
        IReadOnlyDictionary<AssetType, string> suffixes)
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

        return BuildResult(episode, scene, cut, insert, match.Groups["type"].Value, versionTag, suffixes);
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
        VersionTag? versionTag,
        IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var episode = ExtractEpisode(convention);
        var scene = ExtractScene(convention);
        return BuildResult(episode, scene, cut, insertSuffix, typeText, versionTag, suffixes);
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
        VersionTag? versionTag,
        IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var assetType = MapAssetType(typeText, suffixes);
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

    private static AssetType? MapAssetType(string typeText, IReadOnlyDictionary<AssetType, string> suffixes)
    {
        foreach (var pair in MergeTypeSuffixes(suffixes))
        {
            if (string.Equals(pair.Value, typeText, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<AssetType, string> MergeTypeSuffixes(
        IReadOnlyDictionary<AssetType, string> typeSuffixes)
    {
        var merged = new Dictionary<AssetType, string>
        {
            [AssetType.Storyboard] = "分镜",
            [AssetType.Keyframe] = "原画",
            [AssetType.Inbetween] = "动画",
            [AssetType.Background] = "背景",
            [AssetType.Render] = "渲染"
        };

        foreach (var pair in typeSuffixes)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static Regex BuildStandardPattern(IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var typeGroup = RecognitionPatternCompiler.BuildTypeGroup(suffixes);
        return new Regex(
            $"^EP(?<ep>\\d+)_S(?<sc>\\d+)_C(?<cut>\\d+)(?<insert>[a-z]?)_{typeGroup}(?:_(?<version>v\\d+|draft|s))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static Regex BuildSimplePattern(IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var typeGroup = RecognitionPatternCompiler.BuildTypeGroup(suffixes);
        return new Regex(
            $"^C(?<cut>\\d+)(?<insert>[a-z]?)_{typeGroup}(?:_(?<version>v\\d+|draft|s))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static Regex BuildChineseCardPattern(IReadOnlyDictionary<AssetType, string> suffixes)
    {
        var typeGroup = RecognitionPatternCompiler.BuildTypeGroup(suffixes);
        return new Regex($"^(?<cut>\\d+)卡{typeGroup}$", RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"^第(?<cut>\d+)卡$")]
    private static partial Regex OrdinalCardPattern();

    [GeneratedRegex(@"EP(?<value>\d+)")]
    private static partial Regex EpisodeTokenPattern();

    [GeneratedRegex(@"S(?<value>\d+)")]
    private static partial Regex SceneTokenPattern();
}
