using System.Text;

using Generellem.Document.DocumentTypes;
using Generellem.DocumentSource;
using Generellem.Services;

using Microsoft.Extensions.Logging;

namespace Generellem.DocumentSource.Tests;

public class McpResourceMapperTests
{
    const string Server = "notes";
    const long DefaultMaxBytes = 1024;

    [Fact]
    public void Map_Text_JoinsBlocksAndUsesTextType()
    {
        McpResourceMapResult result = Map("notes/today", new McpResourceRead
        {
            Contents =
            [
                new McpResourceContent { Text = "alpha" },
                new McpResourceContent { Text = "beta" }
            ]
        });

        AssertReference(result, "notes/today");
        Assert.False(result.Skipped);
        Assert.Null(result.SkipReason);
        Assert.IsType<Text>(result.Document!.DocType);
        Assert.Equal("A page", result.Document.PathDescription);
        Assert.Equal(0, result.Document.DocStream!.Position);
        Assert.Equal("alpha\nbeta", ReadText(result.Document.DocStream));
    }

    [Fact]
    public void Map_Html_UsesHtmlType()
    {
        const string html = "<p>Hello</p>";

        McpResourceMapResult result = Map("pages/home", new McpResourceRead
        {
            Contents =
            [
                new McpResourceContent { MimeType = "text/html; charset=utf-8", Text = html }
            ]
        });

        AssertReference(result, "pages/home");
        Assert.IsType<Html>(result.Document!.DocType);
        Assert.Equal(html, ReadText(result.Document.DocStream!));
    }

    [Fact]
    public void Map_Markdown_UsesMarkdownType()
    {
        McpResourceMapResult result = Map("pages/readme", new McpResourceRead
        {
            Contents =
            [
                new McpResourceContent { MimeType = "text/markdown", Text = "# Title" }
            ]
        });

        AssertReference(result, "pages/readme");
        Assert.IsType<Markdown>(result.Document!.DocType);
        Assert.Equal("# Title", ReadText(result.Document.DocStream!));
    }

    [Fact]
    public void Map_PdfBlobChosenByMimeType_ReturnsPdfBytes()
    {
        byte[] pdf = "%PDF-1.4 sample"u8.ToArray();

        McpResourceMapResult result = Map(
            "docs/handbook",
            new McpResourceRead
            {
                Contents =
                [
                    new McpResourceContent { MimeType = "text/plain", Text = "plain preview" },
                    new McpResourceContent { MimeType = "application/pdf", BlobBase64 = Convert.ToBase64String(pdf) }
                ]
            },
            pdf.Length);

        Assert.Empty(Path.GetExtension("docs/handbook"));
        AssertReference(result, "docs/handbook");
        Assert.IsType<Pdf>(result.Document!.DocType);
        Assert.Equal(pdf, ReadBytes(result.Document.DocStream!));
    }

    [Fact]
    public void Map_UnknownMime_UsesUriExtension()
    {
        const string uri = "guides/overview.pdf?version=1";
        byte[] body = [1, 2, 3, 4];

        McpResourceMapResult result = Map(uri, new McpResourceRead
        {
            Contents =
            [
                new McpResourceContent
                {
                    MimeType = "application/octet-stream",
                    BlobBase64 = Convert.ToBase64String(body)
                }
            ]
        });

        AssertReference(result, uri);
        Assert.IsType<Pdf>(result.Document!.DocType);
        Assert.Equal(body, ReadBytes(result.Document.DocStream!));
    }

    [Fact]
    public void Map_ResourceMime_SelectsPdfWhenContentMimeMissing()
    {
        byte[] pdf = "%PDF"u8.ToArray();

        McpResourceMapResult result = McpResourceMapper.Map(
            Server,
            new McpListedResource
            {
                Uri = "docs/handbook",
                Name = "Page",
                Description = "A page",
                MimeType = "application/pdf"
            },
            new McpResourceRead
            {
                Contents = [new McpResourceContent { BlobBase64 = Convert.ToBase64String(pdf) }]
            },
            DefaultMaxBytes);

        Assert.Empty(Path.GetExtension("docs/handbook"));
        AssertReference(result, "docs/handbook");
        Assert.IsType<Pdf>(result.Document!.DocType);
        Assert.Equal(pdf, ReadBytes(result.Document.DocStream!));
    }

    [Fact]
    public void Map_ExtensionlessUri_ReturnsUnknown()
    {
        byte[] body = [9, 9];

        McpResourceMapResult result = Map("wiki/home", new McpResourceRead
        {
            Contents =
            [
                new McpResourceContent
                {
                    MimeType = "application/octet-stream",
                    BlobBase64 = Convert.ToBase64String(body)
                }
            ]
        });

        AssertReference(result, "wiki/home");
        Assert.False(result.Skipped);
        Assert.IsType<Unknown>(result.Document!.DocType);
        Assert.Equal($"mcp:{Server}@wiki/home", result.Document.DocumentReference);
        Assert.Equal(body, ReadBytes(result.Document.DocStream!));
    }

    [Fact]
    public void Map_OversizedBlob_IsSeenAndSkipped()
    {
        const string marker = "DO_NOT_LOG_THIS_BLOB";
        byte[] body = Encoding.UTF8.GetBytes(marker);
        ListLogger logger = new();

        McpResourceMapResult result = Map(
            "docs/big.bin",
            new McpResourceRead
            {
                Contents =
                [
                    new McpResourceContent
                    {
                        MimeType = "application/pdf",
                        BlobBase64 = Convert.ToBase64String(body)
                    },
                    new McpResourceContent { MimeType = "text/plain", Text = "preview" }
                ]
            },
            body.Length - 1,
            logger);

        AssertReference(result, "docs/big.bin");
        Assert.True(result.Skipped);
        Assert.Null(result.Document);
        Assert.Equal(McpResourceSkipReason.OversizedBlob, result.SkipReason);
        Assert.Equal($"mcp:{Server}@docs/big.bin", result.DocumentReference);

        Assert.Single(logger.Entries);
        LogEntry entry = logger.Entries[0];
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(GenerellemLogEvents.DocumentError, entry.EventId);
        Assert.Contains(Server, entry.Message, StringComparison.Ordinal);
        Assert.Contains("docs/big.bin", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(body), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("preview", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_ReadWithNoContents_IsSeenAndSkipped()
    {
        ListLogger logger = new();

        McpResourceMapResult result = Map("wiki/missing", new McpResourceRead(), logger: logger);

        AssertReference(result, "wiki/missing");
        Assert.True(result.Skipped);
        Assert.Null(result.Document);
        Assert.Equal(McpResourceSkipReason.NoContents, result.SkipReason);
        Assert.Equal($"mcp:{Server}@wiki/missing", result.DocumentReference);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void ServerReference_UsesServerName()
    {
        Assert.Equal("mcp:notes", McpResourceMapper.ServerReference(Server));
    }

    static McpResourceMapResult Map(
        string uri,
        McpResourceRead read,
        long maxBlobBytes = DefaultMaxBytes,
        ILogger? logger = null) =>
        McpResourceMapper.Map(
            Server,
            new McpListedResource
            {
                Uri = uri,
                Name = "Page",
                Description = "A page"
            },
            read,
            maxBlobBytes,
            logger);

    static void AssertReference(McpResourceMapResult result, string uri)
    {
        Assert.Equal(uri, result.Uri);
        Assert.Equal($"mcp:{Server}@{uri}", result.DocumentReference);
        Assert.StartsWith(McpResourceMapper.ServerReference(Server) + "@", result.DocumentReference, StringComparison.Ordinal);
        if (result.Document is not null)
        {
            Assert.Equal(result.DocumentReference, result.Document.DocumentReference);
            Assert.Equal(uri, result.Document.DocPath);
        }
    }

    static string ReadText(Stream stream)
    {
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    static byte[] ReadBytes(Stream stream)
    {
        stream.Position = 0;
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
