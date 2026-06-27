namespace CutLab.Domain.ValueObjects;

public readonly record struct WorkspacePath(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct FilePath(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct FileName(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct RecognitionPattern(string Pattern);

public readonly record struct MissingCut(CutNumber CutNumber, string Reason);

public readonly record struct MissingInsertSuffix(CutNumber CutNumber, string MissingSuffix, string Reason);

public readonly record struct VersionTag(string Value);

public enum AssetType
{
    Storyboard,
    Keyframe,
    Inbetween,
    Background,
    Render
}

public enum RecognitionStatus
{
    Recognized,
    Unrecognized,
    Conflict
}

public enum CutProductionStatus
{
    Pending,
    InProgress,
    Completed
}

public enum BatchOperationType
{
    Rename,
    Move,
    CreateDirectories
}

public enum BatchStatus
{
    Pending,
    Applied,
    Undone
}

public enum OperationKind
{
    Rename,
    Move,
    CreateDirectory
}
