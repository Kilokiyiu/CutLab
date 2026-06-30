namespace CutLab.Domain.Services;

using CutLab.Domain.ValueObjects;

public interface IFrameSequenceFileParser
{
    ParsedFrameFile? TryParse(
        string filePath,
        FrameSequenceSettings settings,
        NamingConvention namingConvention);
}
