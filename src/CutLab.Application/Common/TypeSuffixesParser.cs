namespace CutLab.Application.Common;

using CutLab.Domain.ValueObjects;

public static class TypeSuffixesParser
{
    private static readonly AssetType[] OrderedTypes =
    [
        AssetType.Storyboard,
        AssetType.Keyframe,
        AssetType.Inbetween,
        AssetType.Background,
        AssetType.Render
    ];

    public static IReadOnlyDictionary<AssetType, string> Default() =>
        new Dictionary<AssetType, string>
        {
            [AssetType.Storyboard] = "分镜",
            [AssetType.Keyframe] = "原画",
            [AssetType.Inbetween] = "动画",
            [AssetType.Background] = "背景",
            [AssetType.Render] = "渲染"
        };

    public static IReadOnlyDictionary<AssetType, string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Default();
        }

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var defaults = Default();
        var result = new Dictionary<AssetType, string>();

        for (var index = 0; index < OrderedTypes.Length; index++)
        {
            var type = OrderedTypes[index];
            result[type] = index < parts.Length && !string.IsNullOrWhiteSpace(parts[index])
                ? parts[index]
                : defaults[type];
        }

        return result;
    }

    public static string Format(IReadOnlyDictionary<AssetType, string> suffixes) =>
        string.Join(", ", OrderedTypes.Select(type =>
            suffixes.TryGetValue(type, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : Default()[type]));

    public static IReadOnlyDictionary<AssetType, string> FromTemplateDictionary(
        IReadOnlyDictionary<string, string>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            return Default();
        }

        return new Dictionary<AssetType, string>
        {
            [AssetType.Storyboard] = GetTemplateValue(dictionary, "storyboard", "分镜"),
            [AssetType.Keyframe] = GetTemplateValue(dictionary, "keyframe", "原画"),
            [AssetType.Inbetween] = GetTemplateValue(dictionary, "inbetween", "动画"),
            [AssetType.Background] = GetTemplateValue(dictionary, "background", "背景"),
            [AssetType.Render] = GetTemplateValue(dictionary, "render", "渲染")
        };
    }

    public static List<string> ToOrderedList(IReadOnlyDictionary<AssetType, string> suffixes) =>
        OrderedTypes.Select(type => suffixes.TryGetValue(type, out var value) ? value : Default()[type]).ToList();

    public static Dictionary<string, string> ToTemplateDictionary(IReadOnlyDictionary<AssetType, string> suffixes) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["storyboard"] = suffixes.TryGetValue(AssetType.Storyboard, out var storyboard) ? storyboard : "分镜",
            ["keyframe"] = suffixes.TryGetValue(AssetType.Keyframe, out var keyframe) ? keyframe : "原画",
            ["inbetween"] = suffixes.TryGetValue(AssetType.Inbetween, out var inbetween) ? inbetween : "动画",
            ["background"] = suffixes.TryGetValue(AssetType.Background, out var background) ? background : "背景",
            ["render"] = suffixes.TryGetValue(AssetType.Render, out var render) ? render : "渲染"
        };

    private static string GetTemplateValue(
        IReadOnlyDictionary<string, string> dictionary,
        string key,
        string fallback) =>
        dictionary.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
}
