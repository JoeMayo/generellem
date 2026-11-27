namespace Generellem.DocumentSource;

/// <summary>
/// Why a listed resource produced no <see cref="DocumentInfo"/>. The URI was still seen.
/// </summary>
public enum McpResourceSkipReason
{
    /// <summary>
    /// A blob decoded to more bytes than the configured cap.
    /// </summary>
    OversizedBlob,

    /// <summary>
    /// The read had no text, no in-limit blob, and no unknown-type blob.
    /// </summary>
    NoContents
}

/// <summary>
/// A listed resource turned into a document, or into a URI that ingestion must still treat as seen.
/// </summary>
public sealed class McpResourceMapResult
{
    McpResourceMapResult(string uri, string documentReference, DocumentInfo? document, McpResourceSkipReason? skipReason)
    {
        Uri = uri;
        DocumentReference = documentReference;
        Document = document;
        SkipReason = skipReason;
    }

    /// <summary>
    /// Resource URI. Set for a document and for a skipped resource.
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// <c>mcp:{name}@{uri}</c>. Set even when <see cref="Document"/> is null, so a skip can be recorded as seen.
    /// </summary>
    public string DocumentReference { get; }

    /// <summary>
    /// Document to hand to ingestion, or null when the resource was skipped.
    /// An <see cref="Document.DocumentTypes.Unknown"/> document is returned so the caller still has the URI.
    /// Ingestion skips that type and does not index it.
    /// </summary>
    public DocumentInfo? Document { get; }

    /// <summary>
    /// Set only when <see cref="Document"/> is null.
    /// </summary>
    public McpResourceSkipReason? SkipReason { get; }

    /// <summary>
    /// True when the URI was seen and no document should be indexed.
    /// </summary>
    public bool Skipped => Document is null;

    /// <summary>
    /// A mapped document. <see cref="DocumentInfo.DocumentReference"/> is the seen reference.
    /// </summary>
    public static McpResourceMapResult ForDocument(string uri, DocumentInfo document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(document);
        return new McpResourceMapResult(uri, document.DocumentReference, document, null);
    }

    /// <summary>
    /// A URI that was seen and must not be indexed. <paramref name="documentReference"/> is <c>mcp:{name}@{uri}</c>.
    /// </summary>
    public static McpResourceMapResult ForSkipped(string uri, string documentReference, McpResourceSkipReason reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentReference);
        return new McpResourceMapResult(uri, documentReference, null, reason);
    }
}
