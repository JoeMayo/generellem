namespace Generellem.Document.DocumentTypes;

public class Image : IDocumentType
{
    readonly IImageTextExtractor extractor;

    public Image()
        : this(ImageText.Current)
    {
    }

    public Image(IImageTextExtractor extractor)
    {
        this.extractor = extractor;
    }

    public virtual bool CanProcess => true;

    public virtual List<string> SupportedExtensions => [".jpg", ".jpeg", ".png", ".gif", ".bmp"];

    public virtual async Task<string> GetTextAsync(Stream documentStream, string fileName) =>
        await extractor.ExtractAsync(documentStream, fileName, CancellationToken.None);
}
