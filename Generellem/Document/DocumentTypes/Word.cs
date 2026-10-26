using System.Text;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using NPOI.HWPF;
using NPOI.HWPF.Extractor;

namespace Generellem.Document.DocumentTypes;

public class Word : IDocumentType
{
    static Word()
    {
        // NPOI HWPF (.doc) needs Windows-1252 and other code pages, which are not
        // available by default on .NET Core / .NET 5+.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public virtual bool CanProcess => true;

    public virtual List<string> SupportedExtensions => new() { ".doc", ".docx" };

    public virtual async Task<string> GetTextAsync(Stream documentStream, string filePath)
    {
        string extension = Path.GetExtension(filePath);

        string text = extension.ToLower() switch
        {
            ".docx" => ReadDocx(documentStream),
            ".doc" => ReadDoc(documentStream),
            _ => throw new ArgumentException("Unsupported file format"),
        };

        return await Task.FromResult(text);
    }

    private static string ReadDoc(Stream fileStream)
    {
        HWPFDocument doc = new(fileStream);
        WordExtractor wordExtractor = new(doc);

        return string.Join(Environment.NewLine, wordExtractor.ParagraphText);
    }

    protected virtual string ReadDocx(Stream fileStream)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Open(fileStream, false);

        Body? body = doc?.MainDocumentPart?.Document?.Body;
        IEnumerable<Paragraph>? paragraphs = body?.Elements<Paragraph>();

        return string.Join(Environment.NewLine, paragraphs?.Select(p => p.InnerText) ?? new List<string>());
    }
}
