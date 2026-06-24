namespace CutLab.Infrastructure.FileSystem;

using CutLab.Application.Common.Interfaces;
using CutLab.Domain.Operations;
using CutLab.Domain.ValueObjects;

internal sealed class LocalFileSystemGateway : IFileSystemGateway
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".psd", ".pdf", ".clip" };

    public async IAsyncEnumerable<FilePath> EnumerateFilesAsync(
        WorkspacePath root,
        bool recursive,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root.Value))
        {
            yield break;
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.EnumerateFiles(root.Value, "*.*", option))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SupportedExtensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            yield return new FilePath(file);
        }

        await Task.CompletedTask;
    }

    public Task ApplyOperationsAsync(
        OperationBatch batch,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("批量文件操作将在后续迭代实现。");
    }

    public Task RevertOperationsAsync(OperationBatch batch, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("撤销操作将在后续迭代实现。");
}
