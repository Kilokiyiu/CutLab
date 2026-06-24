namespace CutLab.Domain.Services;

using CutLab.Domain.Common;
using CutLab.Domain.ValueObjects;

public interface INamingService
{
    Result<FileName> GenerateFileName(
        NamingConvention convention,
        CutNumber cut,
        AssetType type,
        string extension);
}

public sealed record RecognitionResult(
    CutNumber? CutNumber,
    AssetType? AssetType,
    RecognitionStatus Status,
    string? Reason);

public interface IRecognitionService
{
    RecognitionResult TryParse(
        string fileName,
        IReadOnlyList<RecognitionPattern> patterns,
        NamingConvention defaultConvention);
}

public interface IGapAnalysisService
{
    IReadOnlyList<MissingCut> Analyze(
        CutScope scope,
        IReadOnlyList<CutNumber> registeredCuts,
        IReadOnlyList<FilePath> unrecognizedFiles);
}

public interface IArchivePathResolver
{
    Result<FilePath> ResolveDirectory(
        ArchiveTemplate template,
        CutNumber cut,
        AssetType? assetType);
}
