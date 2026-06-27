namespace CutLab.App.Services;

public static class ImagePreviewSupport
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public static bool IsImageFile(string filePath) =>
        ImageExtensions.Contains(Path.GetExtension(filePath));
}
