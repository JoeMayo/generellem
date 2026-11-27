using System.Text;

using Generellem.Document;
using Generellem.Document.DocumentTypes;
using Generellem.Services;

using Microsoft.Extensions.Logging;

namespace Generellem.DocumentSource;

/// <summary>
/// Maps one listed MCP resource and its read result to a <see cref="DocumentInfo"/>
/// or to a seen URI with no document. Does not connect to a server.
/// </summary>
public static class McpResourceMapper
{
    /// <summary>
    /// MIME types for extensions <see cref="DocumentTypeFactory"/> already processes.
    /// An unknown MIME type falls through to the URI extension.
    /// </summary>
    static readonly Dictionary<string, string> MimeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["text/html"] = ".html",
        ["text/markdown"] = ".md",
        ["text/x-markdown"] = ".md",
        ["text/plain"] = ".txt",
        ["text/csv"] = ".csv",
        ["text/tab-separated-values"] = ".tsv",
        ["text/asciidoc"] = ".adoc",
        ["text/x-asciidoc"] = ".adoc",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/bmp"] = ".bmp",
    };

    /// <summary>
    /// Document-source prefix for one configured server: <c>mcp:{name}</c>.
    /// </summary>
    public static string ServerReference(string serverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        return $"mcp:{serverName}";
    }

    /// <summary>
    /// Turns <paramref name="resource"/> and <paramref name="read"/> into a document or a seen-but-skipped URI.
    /// </summary>
    /// <param name="serverName">Configured server name. The reference prefix is <c>mcp:{serverName}</c>.</param>
    /// <param name="resource">One item from <c>resources/list</c>.</param>
    /// <param name="read">The <c>resources/read</c> result for <paramref name="resource"/>.</param>
    /// <param name="maxBlobBytes">Maximum decoded blob size. A larger processable blob skips the resource.</param>
    /// <param name="logger">Receives a warning for an oversized blob. Document text and blob bytes are not logged.</param>
    public static McpResourceMapResult Map(
        string serverName,
        McpListedResource resource,
        McpResourceRead read,
        long maxBlobBytes,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource.Uri);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBlobBytes);

        string serverReference = ServerReference(serverName);
        string documentReference = $"{serverReference}@{resource.Uri}";
        IReadOnlyList<McpResourceContent> contents = read.Contents ?? [];

        List<string> textBlocks = [];
        string? firstTextMime = null;
        bool sawText = false;
        (byte[] Bytes, IDocumentType Type)? unknownBlob = null;
        bool skippedOversizedBlob = false;

        foreach (McpResourceContent content in contents)
        {
            if (HasBlob(content))
            {
                if (TryTakeBlob(content, resource, serverName, maxBlobBytes, logger, out byte[] bytes, out IDocumentType type, out bool oversized))
                {
                    // A blob the factory can process is the document. Text on the same resource is not a substitute.
                    if (type.CanProcess)
                        return Document(resource, serverReference, bytes, type);

                    unknownBlob ??= (bytes, type);
                }
                else if (oversized)
                {
                    skippedOversizedBlob = true;
                    // The document itself is too large. Do not index a text preview in its place.
                    if (type.CanProcess)
                        return Skipped(resource, documentReference, McpResourceSkipReason.OversizedBlob);
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(content.Text))
                continue;

            // The first text block's MIME type selects Html, Markdown, or Text for the joined document.
            if (!sawText)
            {
                firstTextMime = content.MimeType;
                sawText = true;
            }

            textBlocks.Add(content.Text);
        }

        if (textBlocks.Count > 0)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(string.Join('\n', textBlocks));
            return Document(resource, serverReference, textBytes, TypeForText(firstTextMime));
        }

        if (unknownBlob is { } blob)
            return Document(resource, serverReference, blob.Bytes, blob.Type);

        if (skippedOversizedBlob)
            return Skipped(resource, documentReference, McpResourceSkipReason.OversizedBlob);

        return Skipped(resource, documentReference, McpResourceSkipReason.NoContents);
    }

    static McpResourceMapResult Document(McpListedResource resource, string serverReference, byte[] bytes, IDocumentType docType)
    {
        DocumentInfo document = new(serverReference, new MemoryStream(bytes), docType, resource.Uri, resource.Description);
        return McpResourceMapResult.ForDocument(resource.Uri, document);
    }

    static McpResourceMapResult Skipped(McpListedResource resource, string documentReference, McpResourceSkipReason reason) =>
        McpResourceMapResult.ForSkipped(resource.Uri, documentReference, reason);

    static bool HasBlob(McpResourceContent content) =>
        !string.IsNullOrWhiteSpace(content.BlobBase64);

    /// <summary>
    /// Decodes a blob that fits under the cap. An oversized blob is logged and <paramref name="oversized"/> is set.
    /// <paramref name="type"/> is assigned either way so the caller can tell a too-large PDF from a too-large unknown blob.
    /// </summary>
    static bool TryTakeBlob(
        McpResourceContent content,
        McpListedResource resource,
        string serverName,
        long maxBlobBytes,
        ILogger? logger,
        out byte[] bytes,
        out IDocumentType type,
        out bool oversized)
    {
        type = TypeForBlob(content.MimeType, resource.MimeType, resource.Uri);
        bytes = [];
        oversized = false;

        if (!TryDecode(content.BlobBase64, maxBlobBytes, out byte[] decoded, out long size, out oversized))
        {
            if (oversized)
                LogOversized(logger, serverName, resource.Uri, size, maxBlobBytes);
            return false;
        }

        bytes = decoded;
        return true;
    }

    static bool TryDecode(string? blobBase64, long maxBlobBytes, out byte[] bytes, out long size, out bool oversized)
    {
        bytes = [];
        size = 0;
        oversized = false;

        if (string.IsNullOrWhiteSpace(blobBase64))
            return false;

        string compact = StripWhitespace(blobBase64);
        if (compact.Length == 0)
            return false;

        // The base64 text is already in memory. Skip the second buffer when it cannot fit.
        if (DefinitelyExceeds(compact, maxBlobBytes))
        {
            oversized = true;
            size = ((long)compact.Length / 4) * 3;
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(compact);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length > maxBlobBytes)
        {
            oversized = true;
            size = decoded.Length;
            return false;
        }

        if (decoded.Length == 0)
            return false;

        bytes = decoded;
        size = decoded.Length;
        return true;
    }

    static bool DefinitelyExceeds(string compact, long maxBlobBytes)
    {
        if (maxBlobBytes >= long.MaxValue / 4)
            return false;

        long limit = (maxBlobBytes / 3) * 4 + 4;
        return compact.Length > limit;
    }

    static void LogOversized(ILogger? logger, string serverName, string uri, long blobBytes, long maxBlobBytes)
    {
        logger?.LogWarning(
            GenerellemLogEvents.DocumentError,
            "Skipping MCP blob from server {ServerName} at {Uri}. Decoded size {BlobBytes} exceeds {MaxBlobBytes}.",
            serverName,
            uri,
            blobBytes,
            maxBlobBytes);
    }

    static IDocumentType TypeForBlob(string? contentMime, string? resourceMime, string uri)
    {
        IDocumentType? fromContent = TypeFromMime(contentMime);
        if (fromContent is { CanProcess: true })
            return fromContent;

        IDocumentType? fromResource = TypeFromMime(resourceMime);
        if (fromResource is { CanProcess: true })
            return fromResource;

        return DocumentTypeFactory.Create(FileNameFromUri(uri));
    }

    static IDocumentType? TypeFromMime(string? mimeType)
    {
        string? media = MediaType(mimeType);
        if (media is null || !MimeExtensions.TryGetValue(media, out string? extension))
            return null;

        return DocumentTypeFactory.Create("resource" + extension);
    }

    /// <summary>
    /// <c>text/html</c> is <see cref="Html"/>, <c>text/markdown</c> is <see cref="Markdown"/>,
    /// and any other <c>text/*</c> or a missing MIME type is <see cref="Text"/>.
    /// </summary>
    static IDocumentType TypeForText(string? mimeType)
    {
        string? media = MediaType(mimeType);
        if (media is null)
            return new Text();

        if (media.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            return new Html();

        if (media.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            || media.Equals("text/x-markdown", StringComparison.OrdinalIgnoreCase))
            return new Markdown();

        return new Text();
    }

    static string? MediaType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;

        string trimmed = mimeType.Trim();
        int separator = trimmed.IndexOf(';');
        if (separator >= 0)
            trimmed = trimmed[..separator];

        trimmed = trimmed.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    static string FileNameFromUri(string uri)
    {
        string path = uri;
        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute))
            path = absolute.AbsolutePath;
        else
        {
            int cut = path.IndexOfAny(['?', '#']);
            if (cut >= 0)
                path = path[..cut];
        }

        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = path;

        string extension = Path.GetExtension(name);
        if (extension.Length == 0)
            return name;

        return string.Concat(Path.GetFileNameWithoutExtension(name), extension.ToLowerInvariant());
    }

    static string StripWhitespace(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsWhiteSpace(c))
                continue;

            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }

        return value;
    }
}
