namespace CutLab.Application.Common;

using CutLab.Domain.Common;
using CutLab.Domain.Scanning;
using CutLab.Domain.ValueObjects;

public static class CutSequenceReorderPlanner
{
    public static IReadOnlyList<int> GetOrderedCutNumbers(
        ScanSession session,
        int episode,
        int scene)
    {
        return session.GetRecognized()
            .Where(asset => asset.ParsedCut is { } cut
                            && cut.Episode == episode
                            && cut.Scene == scene)
            .Select(asset => asset.ParsedCut!.Value.Cut)
            .Distinct()
            .OrderBy(cut => cut)
            .ToList();
    }

    public static Result<IReadOnlyDictionary<CutNumber, CutNumber>> BuildRenumberMap(
        ScanSession session,
        int episode,
        int scene,
        int movedCut,
        int targetIndex)
    {
        var orderedCuts = GetOrderedCutNumbers(session, episode, scene).ToList();
        if (orderedCuts.Count == 0)
        {
            return Result.Failure<IReadOnlyDictionary<CutNumber, CutNumber>>("没有可排序的已识别卡号。");
        }

        var fromIndex = orderedCuts.IndexOf(movedCut);
        if (fromIndex < 0)
        {
            return Result.Failure<IReadOnlyDictionary<CutNumber, CutNumber>>($"卡号 C{movedCut:D3} 不在当前列表中。");
        }

        orderedCuts.RemoveAt(fromIndex);
        targetIndex = Math.Clamp(targetIndex, 0, orderedCuts.Count);
        orderedCuts.Insert(targetIndex, movedCut);

        var startCut = GetOrderedCutNumbers(session, episode, scene).Min();
        var integerMap = new Dictionary<int, int>();
        for (var index = 0; index < orderedCuts.Count; index++)
        {
            integerMap[orderedCuts[index]] = startCut + index;
        }

        var mapping = new Dictionary<CutNumber, CutNumber>();
        var distinctCuts = session.GetRecognized()
            .Where(asset => asset.ParsedCut is { } cut
                            && cut.Episode == episode
                            && cut.Scene == scene)
            .Select(asset => asset.ParsedCut!.Value)
            .Distinct()
            .ToList();

        foreach (var parsedCut in distinctCuts)
        {
            if (!integerMap.TryGetValue(parsedCut.Cut, out var newCut)
                || newCut == parsedCut.Cut)
            {
                continue;
            }

            mapping[parsedCut] = new CutNumber(
                parsedCut.Episode,
                parsedCut.Scene,
                newCut,
                parsedCut.InsertSuffix);
        }

        if (mapping.Count == 0)
        {
            return Result.Failure<IReadOnlyDictionary<CutNumber, CutNumber>>("卡号顺序未发生变化。");
        }

        return Result.Success<IReadOnlyDictionary<CutNumber, CutNumber>>(mapping);
    }
}
