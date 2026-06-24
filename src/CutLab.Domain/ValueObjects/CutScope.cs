namespace CutLab.Domain.ValueObjects;

public readonly record struct CutScope(
    EpisodeNumber Episode,
    int Scene,
    CutNumber From,
    CutNumber To);
