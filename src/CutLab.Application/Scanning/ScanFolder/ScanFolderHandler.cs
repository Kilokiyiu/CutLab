namespace CutLab.Application.Scanning.ScanFolder;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record ScanFolderCommand(ProjectId ProjectId, string SourcePath, bool Recursive);

public sealed record ScanFolderResult(
    Guid SessionId,
    int TotalFiles,
    int RecognizedCount,
    int UnrecognizedCount);

public sealed class ScanFolderHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IFileSystemGateway _fileSystemGateway;
    private readonly IRecognitionService _recognitionService;
    private readonly INamingService _namingService;

    public ScanFolderHandler(
        IAnimationProjectRepository projectRepository,
        IFileSystemGateway fileSystemGateway,
        IRecognitionService recognitionService,
        INamingService namingService)
    {
        _projectRepository = projectRepository;
        _fileSystemGateway = fileSystemGateway;
        _recognitionService = recognitionService;
        _namingService = namingService;
    }

    public async Task<Result<ScanFolderResult>> HandleAsync(
        ScanFolderCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<ScanFolderResult>("项目不存在。");
        }

        var session = ScanSession.Create(command.ProjectId, new WorkspacePath(command.SourcePath));

        await foreach (var file in _fileSystemGateway.EnumerateFilesAsync(
                           session.SourcePath,
                           command.Recursive,
                           cancellationToken))
        {
            var fileName = Path.GetFileName(file.Value);
            var recognition = _recognitionService.TryParse(
                fileName,
                project.RecognitionPatterns,
                project.NamingConvention);

            FileName? proposed = null;
            if (recognition.CutNumber is { } cut && recognition.AssetType is { } type)
            {
                var extension = Path.GetExtension(fileName);
                var naming = _namingService.GenerateFileName(
                    project.NamingConvention,
                    cut,
                    type,
                    extension);

                if (naming.IsSuccess)
                {
                    proposed = naming.Value;
                }
            }

            session.AddDiscoveredAsset(
                file,
                recognition.CutNumber,
                recognition.AssetType,
                proposed,
                recognition.Status);
        }

        return Result.Success(new ScanFolderResult(
            session.Id,
            session.Assets.Count,
            session.GetRecognized().Count,
            session.GetUnrecognized().Count));
    }
}
