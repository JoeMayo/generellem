namespace Generellem.DocumentSource;

/// <summary>
/// One MCP <c>resources/read</c> result for a listed resource.
/// </summary>
public sealed class McpResourceRead
{
    /// <summary>
    /// Content items in server order. Null and empty both mean the read returned nothing.
    /// </summary>
    public IReadOnlyList<McpResourceContent>? Contents { get; init; }
}

/// <summary>
/// One content item. A non-empty <see cref="BlobBase64"/> is a blob and <see cref="Text"/> is ignored.
/// Text is used only when the item has no blob.
/// </summary>
public sealed class McpResourceContent
{
    /// <summary>
    /// MIME type of this item, including an optional <c>; charset</c> parameter.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Already extracted text. Not parsed again as a binary format.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Base64-encoded bytes. The mapper decodes them and compares the decoded length with the blob cap.
    /// </summary>
    public string? BlobBase64 { get; init; }
}
