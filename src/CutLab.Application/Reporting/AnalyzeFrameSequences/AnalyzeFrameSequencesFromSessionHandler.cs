namespace CutLab.Application.Reporting.AnalyzeFrameSequences;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;
using CutLab.Domain.Scanning;
using CutLab.Domain.Services;
using CutLab.Domain.ValueObjects;

public sealed record AnalyzeFrameSequencesFromSessionQuery(ProjectId ProjectId, Guid SessionId);

public sealed class AnalyzeFrameSequencesFromSessionHandler
{
    private readonly IAnimationProjectRepository _projectRepository;
    private readonly IScanSessionRepository _sessionRepository;
    private readonly IFrameSequenceAnalyzer _analyzer;
    private readonly IFrameSequenceFileParser _frameSequenceFileParser;

    public AnalyzeFrameSequencesFromSessionHandler(
        IAnimationProjectRepository projectRepository,
        IScanSessionRepository sessionRepository,
        IFrameSequenceAnalyzer analyzer,
        IFrameSequenceFileParser frameSequenceFileParser)
    {
        _projectRepository = projectRepository;
        _sessionRepository = sessionRepository;
        _analyzer = analyzer;
        _frameSequenceFileParser = frameSequenceFileParser;
    }

    public async Task<Result<FrameSequenceAnalysisResult>> HandleAsync(
        AnalyzeFrameSequencesFromSessionQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(query.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<FrameSequenceAnalysisResult>("项目不存在。");
        }

        if (!project.FrameSequenceSettings.Enabled)
        {
            return Result.Failure<FrameSequenceAnalysisResult>("请先在项目设置中启用帧序列绑定。");
        }

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<FrameSequenceAnalysisResult>("扫描会话不存在。");
        }

        if (session.ProjectId != query.ProjectId)
        {
            return Result.Failure<FrameSequenceAnalysisResult>("扫描会话与项目不匹配。");
        }

        var parsedFiles = new List<ParsedFrameFile>();
        foreach (var asset in session.Assets)
        {
            if (_frameSequenceFileParser.TryParse(
                    asset.OriginalPath.Value,
                    project.FrameSequenceSettings,
                    project.NamingConvention) is { } parsed)
            {
                parsedFiles.Add(parsed);
            }
        }

        if (parsedFiles.Count == 0)
        {
            return Result.Failure<FrameSequenceAnalysisResult>("未找到符合帧序列模板的文件。");
        }

        return Result.Success(_analyzer.Analyze(project.FrameSequenceSettings, parsedFiles));
    }
}
