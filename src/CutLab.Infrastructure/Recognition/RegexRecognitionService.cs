namespace CutLab.Infrastructure.Recognition;

using System.Text.RegularExpressions;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

internal sealed partial class RegexRecognitionService : IRecognitionService
{
    public RecognitionResult TryParse(
        string fileName,
        IReadOnlyList<RecognitionPattern> patterns,
        NamingConvention defaultConvention)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        if (ChineseCardPattern().Match(nameWithoutExtension) is { Success: true } chineseMatch)
        {
            var cut = int.Parse(chineseMatch.Groups["cut"].Value);
            var typeText = chineseMatch.Groups["type"].Value;
            return BuildResult(1, 1, cut, typeText);
        }

        if (StandardCutPattern().Match(nameWithoutExtension) is { Success: true } standardMatch)
        {
            var episode = int.Parse(standardMatch.Groups["ep"].Value);
            var scene = int.Parse(standardMatch.Groups["sc"].Value);
            var cut = int.Parse(standardMatch.Groups["cut"].Value);
            var typeText = standardMatch.Groups["type"].Value;
            return BuildResult(episode, scene, cut, typeText);
        }

        return new RecognitionResult(null, null, RecognitionStatus.Unrecognized, "未匹配任何识别规则。");
    }

    private static RecognitionResult BuildResult(int episode, int scene, int cut, string typeText)
    {
        var assetType = MapAssetType(typeText);
        if (assetType is null)
        {
            return new RecognitionResult(
                new CutNumber(episode, scene, cut),
                null,
                RecognitionStatus.Unrecognized,
                $"无法识别资产类型：{typeText}");
        }

        return new RecognitionResult(
            new CutNumber(episode, scene, cut),
            assetType,
            RecognitionStatus.Recognized,
            null);
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

    [GeneratedRegex(@"^(?<cut>\d+)卡(?<type>分镜|原画|动画|背景|渲染)$")]
    private static partial Regex ChineseCardPattern();

    [GeneratedRegex(@"^EP(?<ep>\d+)_S(?<sc>\d+)_C(?<cut>\d+)_(?<type>分镜|原画|动画|背景|渲染)$", RegexOptions.IgnoreCase)]
    private static partial Regex StandardCutPattern();
}
