namespace CutLab.Domain.Cuts;

using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed class Cut : Entity<Guid>
{
    internal Cut(CutNumber cutNumber, CutProductionStatus status, string? note, VersionTag? versionTag)
    {
        Id = Guid.NewGuid();
        CutNumber = cutNumber;
        Status = status;
        Note = note;
        VersionTag = versionTag;
    }

    public CutNumber CutNumber { get; private set; }

    public CutProductionStatus Status { get; private set; }

    public string? Note { get; private set; }

    public VersionTag? VersionTag { get; private set; }

    public void MarkStatus(CutProductionStatus status) => Status = status;
}

public sealed class CutRegistry : AggregateRoot<Guid>
{
    private readonly List<Cut> _cuts = [];

    private CutRegistry(Guid id, ProjectId projectId, CutScope scope)
    {
        Id = id;
        ProjectId = projectId;
        Scope = scope;
    }

    public ProjectId ProjectId { get; }

    public CutScope Scope { get; }

    public IReadOnlyList<Cut> Cuts => _cuts.AsReadOnly();

    public static CutRegistry Create(ProjectId projectId, CutScope scope) =>
        new(Guid.NewGuid(), projectId, scope);

    public Result RegisterCut(CutNumber cutNumber)
    {
        if (!cutNumber.IsWithin(Scope))
        {
            return Result.Failure("卡号不在当前范围内。");
        }

        if (_cuts.Any(c => c.CutNumber == cutNumber))
        {
            return Result.Failure($"卡号 {cutNumber} 已存在。");
        }

        _cuts.Add(new Cut(cutNumber, CutProductionStatus.Pending, null, null));
        return Result.Success();
    }

    public Result<CutNumber> CreateInsertCut(int afterCut, int episode, int scene, IEnumerable<string?> existingInsertSuffixes)
    {
        var suffix = CutInsertSuffixGenerator.Next(existingInsertSuffixes);
        var cutNumber = new CutNumber(episode, scene, afterCut, suffix);
        return RegisterCut(cutNumber).IsSuccess
            ? Result.Success(cutNumber)
            : Result.Failure<CutNumber>($"无法注册插卡 {cutNumber}。");
    }

    public IReadOnlyList<MissingCut> DetectGaps()
    {
        var missing = new List<MissingCut>();

        for (var cut = Scope.From.Cut; cut <= Scope.To.Cut; cut++)
        {
            var candidate = new CutNumber(Scope.Episode.Value, Scope.Scene, cut);
            if (_cuts.All(c => c.CutNumber.Cut != cut))
            {
                missing.Add(new MissingCut(candidate, "范围内无对应镜头。"));
            }
        }

        return missing;
    }

    public IReadOnlyList<MissingInsertSuffix> DetectInsertSuffixGaps()
    {
        var missing = new List<MissingInsertSuffix>();
        var groups = _cuts
            .GroupBy(cut => (cut.CutNumber.Episode, cut.CutNumber.Scene, cut.CutNumber.Cut));

        foreach (var group in groups)
        {
            var usedSuffixes = group
                .Select(cut => cut.CutNumber.InsertSuffix)
                .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
                .Select(suffix => suffix![0])
                .ToHashSet();

            if (usedSuffixes.Count == 0)
            {
                continue;
            }

            var maxSuffix = usedSuffixes.Max();
            for (var letter = 'b'; letter <= maxSuffix; letter++)
            {
                if (!usedSuffixes.Contains(letter))
                {
                    missing.Add(new MissingInsertSuffix(
                        new CutNumber(group.Key.Episode, group.Key.Scene, group.Key.Cut, letter.ToString()),
                        letter.ToString(),
                        $"C{group.Key.Cut:D3} 缺少插卡后缀 {letter}。"));
                }
            }
        }

        return missing;
    }
}
