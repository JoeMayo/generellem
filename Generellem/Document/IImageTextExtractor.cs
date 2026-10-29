namespace Generellem.Document;

/// <summary>
/// Reads text out of an image during ingestion.
/// </summary>
public interface IImageTextExtractor
{
    Task<string> ExtractAsync(Stream image, string fileName, CancellationToken cancellationToken);
}
