namespace CutLab.App.ViewModels;

using Avalonia.Media.Imaging;

public sealed class CutGalleryItemViewModel
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public CutGalleryItemViewModel(string cutId, string filePath)
    {
        CutId = cutId;
        FilePath = filePath;
        Thumbnail = TryLoadThumbnail(filePath);
    }

    public string CutId { get; }

    public string FilePath { get; }

    public Bitmap? Thumbnail { get; }

    private static Bitmap? TryLoadThumbnail(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!ImageExtensions.Contains(extension))
        {
            return null;
        }

        try
        {
            return new Bitmap(filePath);
        }
        catch
        {
            return null;
        }
    }
}
