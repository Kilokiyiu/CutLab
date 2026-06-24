namespace CutLab.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
