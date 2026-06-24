namespace CutLab.Domain.Projects;

public interface IAnimationProjectRepository
{
    Task<AnimationProject?> GetByIdAsync(ProjectId id, CancellationToken cancellationToken = default);

    Task SaveAsync(AnimationProject project, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnimationProject>> ListRecentAsync(int count, CancellationToken cancellationToken = default);
}
