namespace MoE.ModelManager.Services;

internal static class ImageDataUrlFactory
{
    internal const long MaximumImageBytes = 20L * 1024 * 1024;

    internal static async Task<string> CreateAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        FileInfo file = new(imagePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The selected chat image was not found.", imagePath);
        }

        if (file.Length > MaximumImageBytes)
        {
            throw new InvalidDataException("The selected image exceeds the 20 MB attachment limit.");
        }

        string mediaType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => throw new InvalidDataException("Select a PNG, JPEG, WebP, BMP, or GIF image."),
        };
        byte[] data = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return $"data:{mediaType};base64,{Convert.ToBase64String(data)}";
    }
}
