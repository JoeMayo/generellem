namespace Generellem.Document;

/// <summary>
/// Extractor used by <see cref="DocumentTypes.Image"/>, which the document factory constructs without DI.
/// Startup replaces <see cref="Current"/> before ingestion runs.
/// </summary>
public static class ImageText
{
    public static IImageTextExtractor Current { get; set; } = new EmbeddedImageTextExtractor();
}
