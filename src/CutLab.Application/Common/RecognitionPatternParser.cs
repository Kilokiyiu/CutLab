namespace CutLab.Application.Common;

using CutLab.Domain.ValueObjects;

public static class RecognitionPatternParser
{
    public static IReadOnlyList<RecognitionPattern> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pattern => new RecognitionPattern(pattern))
                .ToList();

    public static string Format(IReadOnlyList<RecognitionPattern> patterns) =>
        patterns.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, patterns.Select(pattern => pattern.Pattern));
}
