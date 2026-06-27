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
        var entries = batch.Entries.ToList();
        var completed = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (entry.Kind)
                {
                    case OperationKind.Rename:
                        ApplyRename(entry);
                        entry.MarkSuccess();
                        break;
                    case OperationKind.Move:
                        ApplyMove(entry);
                        entry.MarkSuccess();
                        break;
                    case OperationKind.CreateDirectory:
                        Directory.CreateDirectory(entry.TargetPath.Value);
                        entry.MarkSuccess();
                        break;
                    default:
                        entry.MarkFailed();
                        break;
                }
            }
            catch
            {
                entry.MarkFailed();
            }

            completed++;
            progress?.Report(new OperationProgress(completed, entries.Count, entry.SourcePath.Value));
        }

        return Task.CompletedTask;
    }

    public Task RevertOperationsAsync(OperationBatch batch, CancellationToken cancellationToken = default)
    {
        foreach (var entry in batch.GetSuccessfulEntries().Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (entry.Kind)
            {
                case OperationKind.Rename:
                case OperationKind.Move:
                    if (File.Exists(entry.TargetPath.Value))
                    {
                        File.Move(entry.TargetPath.Value, entry.SourcePath.Value, overwrite: true);
                    }

                    break;
                case OperationKind.CreateDirectory:
                    if (Directory.Exists(entry.TargetPath.Value))
                    {
                        Directory.Delete(entry.TargetPath.Value, recursive: false);
                    }

                    break;
            }
        }

        return Task.CompletedTask;
    }

    private static void ApplyRename(FileOperationEntry entry)
    {
        if (!File.Exists(entry.SourcePath.Value))
        {
            throw new FileNotFoundException("源文件不存在。", entry.SourcePath.Value);
        }

        if (File.Exists(entry.TargetPath.Value))
        {
            throw new IOException($"目标文件已存在：{entry.TargetPath.Value}");
        }

        var targetDirectory = Path.GetDirectoryName(entry.TargetPath.Value);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.Move(entry.SourcePath.Value, entry.TargetPath.Value);
    }

    private static void ApplyMove(FileOperationEntry entry)
    {
        if (!File.Exists(entry.SourcePath.Value))
        {
            throw new FileNotFoundException("源文件不存在。", entry.SourcePath.Value);
        }

        if (File.Exists(entry.TargetPath.Value))
        {
            throw new IOException($"目标文件已存在：{entry.TargetPath.Value}");
        }

        var targetDirectory = Path.GetDirectoryName(entry.TargetPath.Value);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.Move(entry.SourcePath.Value, entry.TargetPath.Value);
    }
}
