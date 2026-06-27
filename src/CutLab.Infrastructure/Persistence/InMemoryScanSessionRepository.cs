namespace CutLab.Infrastructure.Persistence;

using CutLab.Domain.Scanning;

internal sealed class InMemoryScanSessionRepository : IScanSessionRepository
{
    private readonly Dictionary<Guid, ScanSession> _store = [];

    public Task SaveAsync(ScanSession session, CancellationToken cancellationToken = default)
    {
        _store[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<ScanSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }
}
