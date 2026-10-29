using System.Reflection;

using Generellem.Document.DocumentTypes;

namespace Generellem.Document;

/// <summary>
/// Creates <see cref="IDocumentType"/> instances.
/// </summary>
public class DocumentTypeFactory
{
    static IEnumerable<string>? supportedDocTypes = null;

    /// <summary>
    /// Returns an <see cref="IDocumentType"/> based on file extension.
    /// </summary>
    /// <param name="fileName">Name of file + extension.</param>
    /// <returns><see cref="IDocumentType"/></returns>
    public static IDocumentType Create(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension switch
        {
            ".adoc" or ".asc" or ".asciidoc" => new AsciiDoc(),
            ".csv" => new Csv(),
            ".doc" or ".docx" => new Word(),
            ".html" => new Html(),
            ".md" => new Markdown(),
            ".pdf" => new Pdf(),
            ".pptx" /*or ".ppt"*/ => new Powerpoint(),
            ".tsv" => new Tsv(),
            ".txt" => new Text(),
            ".xlsx" or ".xls" => new Excel(),
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => new Image(),
            _ => new Unknown(),
        };
    }

    /// <summary>
    /// Use reflection to find all <see cref="IDocumentType"/> implementations 
    /// and extract which document types (by extension) they support.
    /// </summary>
    /// <returns><see cref="IEnumerable{T}"/> of <see cref="string"/></returns>
    public static IEnumerable<string> GetSupportedDocumentTypes()
    {
        supportedDocTypes ??= 
            (from docType in Assembly.GetExecutingAssembly().GetTypes()
             where docType.GetInterfaces().Contains(typeof(IDocumentType))
             let docTypeInstance = Activator.CreateInstance(docType) as IDocumentType
             where docTypeInstance.CanProcess
             from docExtension in docTypeInstance.SupportedExtensions
             select docExtension)
            .ToList();

        return supportedDocTypes ?? new List<string>();
    }
}