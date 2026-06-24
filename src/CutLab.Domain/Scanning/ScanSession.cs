namespace CutLab.Domain.Scanning;

using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.ValueObjects;

public sealed class ProductionAsset : Entity<Guid>
{
    internal ProductionAsset(
        FilePath originalPath,
        CutNumber? parsedCut,
        AssetType? assetType,
        FileName? proposedFileName,
        RecognitionStatus recognitionStatus,
        Guid? conflictWith)
    {
        Id = Guid.NewGuid();
        OriginalPath = originalPath;
        ParsedCut = parsedCut;
        AssetType = assetType;
        ProposedFileName = proposedFileName;
        RecognitionStatus = recognitionStatus;
        ConflictWith = conflictWith;
    }

    public FilePath OriginalPath { get; }

    public CutNumber? ParsedCut { get; }

    public AssetType? AssetType { get; }

    public FileName? ProposedFileName { get; }

    public RecognitionStatus RecognitionStatus { get; }

    public Guid? ConflictWith { get; }
}

public sealed class ScanSession : AggregateRoot<Guid>
{
    private readonly List<ProductionAsset> _assets = [];

    private ScanSession(Guid id, ProjectId projectId, WorkspacePath sourcePath)
    {
        Id = id;
        ProjectId = projectId;
        SourcePath = sourcePath;
        ScannedAt = DateTimeOffset.UtcNow;
    }

    public ProjectId ProjectId { get; }

    public WorkspacePath SourcePath { get; }

    public DateTimeOffset ScannedAt { get; }

    public IReadOnlyList<ProductionAsset> Assets => _assets.AsReadOnly();

    public static ScanSession Create(ProjectId projectId, WorkspacePath sourcePath) =>
        new(Guid.NewGuid(), projectId, sourcePath);

    public void AddDiscoveredAsset(
        FilePath originalPath,
        CutNumber? parsedCut,
        AssetType? assetType,
        FileName? proposedFileName,
        RecognitionStatus status)
    {
        _assets.Add(new ProductionAsset(
            originalPath,
            parsedCut,
            assetType,
            proposedFileName,
            status,
            null));
    }

    public IReadOnlyList<ProductionAsset> GetRecognized() =>
        _assets.Where(a => a.RecognitionStatus == RecognitionStatus.Recognized).ToList();

    public IReadOnlyList<ProductionAsset> GetUnrecognized() =>
        _assets.Where(a => a.RecognitionStatus == RecognitionStatus.Unrecognized).ToList();
}
