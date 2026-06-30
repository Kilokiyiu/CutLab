using CutLab.Application.Common;
using CutLab.Domain.ValueObjects;

namespace CutLab.Application.Tests;

public class TypeSuffixesParserTests
{
    [Fact]
    public void Parse_ShouldUseCommaSeparatedOrder()
    {
        var suffixes = TypeSuffixesParser.Parse("SB, KF, IB, BG, RD");

        Assert.Equal("SB", suffixes[AssetType.Storyboard]);
        Assert.Equal("KF", suffixes[AssetType.Keyframe]);
        Assert.Equal("IB", suffixes[AssetType.Inbetween]);
        Assert.Equal("BG", suffixes[AssetType.Background]);
        Assert.Equal("RD", suffixes[AssetType.Render]);
    }

    [Fact]
    public void FromTemplateDictionary_ShouldMapJsonKeys()
    {
        var suffixes = TypeSuffixesParser.FromTemplateDictionary(new Dictionary<string, string>
        {
            ["storyboard"] = "分镜稿",
            ["keyframe"] = "原画稿"
        });

        Assert.Equal("分镜稿", suffixes[AssetType.Storyboard]);
        Assert.Equal("原画稿", suffixes[AssetType.Keyframe]);
        Assert.Equal("动画", suffixes[AssetType.Inbetween]);
    }

    [Fact]
    public void Format_ShouldRoundTrip()
    {
        var original = "分镜, 原画, 动画, 背景, 渲染";
        var formatted = TypeSuffixesParser.Format(TypeSuffixesParser.Parse(original));
        Assert.Equal(original, formatted);
    }
}
