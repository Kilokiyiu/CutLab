namespace CutLab.Domain.Cuts;

public static class CutInsertSuffixGenerator
{
    public static string Next(IEnumerable<string?> existingSuffixes)
    {
        var used = existingSuffixes
            .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
            .Select(suffix => suffix!.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var letter = 'b'; letter <= 'z'; letter++)
        {
            var candidate = letter.ToString();
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("插卡后缀已用尽（b-z）。");
    }
}
