namespace CutLab.Domain.ValueObjects;

public sealed record ParsedFrameFile(
    FilePath Path,
    CutNumber? CutNumber,
    int FrameNumber);

public sealed record MissingFrameIssue(CutNumber CutNumber, int FrameNumber, string Reason);

public sealed record DuplicateFrameIssue(
    CutNumber CutNumber,
    int FrameNumber,
    IReadOnlyList<string> FilePaths,
    string Reason);

public sealed record CrossCutFrameIssue(
    int FrameNumber,
    IReadOnlyList<string> Entries,
    string Reason);

public sealed record OrphanFrameIssue(string FilePath, int? FrameNumber, string Reason);

public sealed record FrameSequenceAnalysisResult(
    bool Enabled,
    int ParsedFileCount,
    IReadOnlyList<MissingFrameIssue> MissingFrames,
    IReadOnlyList<DuplicateFrameIssue> DuplicateFrames,
    IReadOnlyList<CrossCutFrameIssue> CrossCutFrames,
    IReadOnlyList<OrphanFrameIssue> OrphanFrames);
