namespace Generellem.Document;

/// <summary>
/// Image ingestion that uses embedded metadata and the file name.
/// </summary>
public sealed class EmbeddedImageTextExtractor : IImageTextExtractor
{
    public async Task<string> ExtractAsync(Stream image, string fileName, CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        await image.CopyToAsync(copy, cancellationToken);
        return EmbeddedImageText.Read(copy.GetBuffer().AsSpan(0, (int)copy.Length), fileName);
    }
}
