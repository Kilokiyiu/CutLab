namespace CutLab.Domain.Scanning;

public interface IScanSessionRepository
{
    Task SaveAsync(ScanSession session, CancellationToken cancellationToken = default);

    Task<ScanSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
