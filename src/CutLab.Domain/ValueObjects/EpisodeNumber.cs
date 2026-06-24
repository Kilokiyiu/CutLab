namespace CutLab.Domain.ValueObjects;

public readonly record struct EpisodeNumber(int Value)
{
    public override string ToString() => Value.ToString("D2");
}
