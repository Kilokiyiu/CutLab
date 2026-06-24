namespace CutLab.Domain.ValueObjects;

public readonly record struct CutNumber(int Episode, int Scene, int Cut, string? InsertSuffix = null)
{
    public override string ToString() =>
        InsertSuffix is null
            ? $"EP{Episode:D2}_S{Scene:D2}_C{Cut:D3}"
            : $"EP{Episode:D2}_S{Scene:D2}_C{Cut:D3}{InsertSuffix}";

    public bool IsWithin(CutScope scope) =>
        Episode == scope.Episode.Value
        && Scene == scope.Scene
        && Cut >= scope.From.Cut
        && Cut <= scope.To.Cut;
}
