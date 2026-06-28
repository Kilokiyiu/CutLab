namespace CutLab.Application.Projects.DeleteProject;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Common;
using CutLab.Domain.Projects;

public sealed record DeleteProjectCommand(ProjectId ProjectId);

public sealed class DeleteProjectHandler
{
    private readonly IAnimationProjectRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProjectHandler(IAnimationProjectRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteProjectCommand command, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure("项目不存在。");
        }

        await _repository.DeleteAsync(command.ProjectId, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
