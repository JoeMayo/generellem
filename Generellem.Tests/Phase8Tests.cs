using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Generellem.Document;
using Generellem.Document.DocumentTypes;
using Generellem.Embedding;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Llm.Local;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;
using Generellem.Services.Azure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

namespace Generellem.Tests;

public class LexicalSparseTests
{
    [Fact]
    public void FromText_RepeatsIncreaseWeightAndSortsIndices()
    {
        (float[] values, uint[] indices) = LexicalSparse.FromText("Alpha alpha beta");

        Assert.Equal(2, indices.Length);
        Assert.Equal(indices, indices.OrderBy(index => index).ToArray());
        Assert.Contains(2f, values);
        Assert.Contains(1f, values);
    }

    [Fact]
    public void FromText_IsStable()
    {
        (float[] firstValues, uint[] firstIndices) = LexicalSparse.FromText("hybrid search");
        (float[] secondValues, uint[] secondIndices) = LexicalSparse.FromText("hybrid search");

        Assert.Equal(firstIndices, secondIndices);
        Assert.Equal(firstValues, secondValues);
    }
}

public class RerankerTests
{
    [Fact]
    public async Task LexicalReranker_MovesMatchingChunkFirst()
    {
        LexicalReranker reranker = new();
        List<TextChunk> chunks =
        [
            new() { ID = "a", Content = "zebra habitat" },
            new() { ID = "b", Content = "alpha applesauce" }
        ];

        List<TextChunk> ranked = await reranker.RerankAsync("alpha", chunks, CancellationToken.None);

        Assert.Equal("b", ranked[0].ID);
    }

    [Fact]
    public async Task LlmReranker_UsesParsedOrder()
    {
        LlmReranker reranker = new(new ScriptedLlm("1, 0"));
        List<TextChunk> chunks =
        [
            new() { ID = "a", Content = "first" },
            new() { ID = "b", Content = "second" }
        ];

        List<TextChunk> ranked = await reranker.RerankAsync("query", chunks, CancellationToken.None);

        Assert.Equal(["b", "a"], ranked.Select(chunk => chunk.ID));
    }

    [Fact]
    public async Task LlmReranker_InvalidReplyKeepsOriginalOrder()
    {
        LlmReranker reranker = new(new ScriptedLlm("no numbers here"));
        List<TextChunk> chunks = [new() { ID = "a", Content = "first" }, new() { ID = "b", Content = "second" }];

        List<TextChunk> ranked = await reranker.RerankAsync("query", chunks, CancellationToken.None);

        Assert.Equal(["a", "b"], ranked.Select(chunk => chunk.ID));
    }

    sealed class ScriptedLlm(string text) : ILlm
    {
        public Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
            where TResponse : IChatResponse
        {
            IChatResponse response = new AzureOpenAIChatResponse(text);
            return Task.FromResult((TResponse)response);
        }

        public async IAsyncEnumerable<string> StreamAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return text;
            await Task.CompletedTask;
        }
    }
}

public class ImageTextTests
{
    [Fact]
    public void Read_PngTextChunk_ReturnsEmbeddedWords()
    {
        byte[] png = PngWithText("Comment", "periwinkle beacon");

        string text = EmbeddedImageText.Read(png, "note.png");

        Assert.Contains("Image file: note.png", text);
        Assert.Contains("periwinkle beacon", text);
    }

    [Fact]
    public void Read_JpegComment_ReturnsEmbeddedWords()
    {
        byte[] jpeg = JpegWithComment("beacon text");

        string text = EmbeddedImageText.Read(jpeg, "scan.jpg");

        Assert.Contains("beacon text", text);
    }

    [Fact]
    public async Task Image_UsesExtractor()
    {
        Image image = new(new FixedExtractor("from the picture"));

        string text = await image.GetTextAsync(new MemoryStream([1, 2, 3]), "pic.png");

        Assert.Equal("from the picture", text);
    }

    [Fact]
    public async Task VisionExtractor_AppendsOllamaDescription()
    {
        HttpClient client = new(new ScriptedHandler("{\"message\":{\"content\":\"a red barn\"}}"));
        Mock<IGenerellemHttpClientFactory> factory = new();
        factory.Setup(item => item.Create()).Returns(client);
        VisionImageTextExtractor extractor = new(
            Options.Create(new DependencyInjection.GenerellemOptions
            {
                Profile = DependencyInjection.GenerellemProfile.Ollama,
                EnableImageVision = true,
                VisionModel = "llava",
                OllamaEndpoint = "http://localhost:11434"
            }),
            factory.Object,
            Mock.Of<ILogger<VisionImageTextExtractor>>());

        string text = await extractor.ExtractAsync(new MemoryStream(PngWithText("Comment", "embedded")), "barn.png", CancellationToken.None);

        Assert.Contains("embedded", text);
        Assert.Contains("a red barn", text);
    }

    [Fact]
    public void MediaType_UsesJpegForJpg()
    {
        Assert.Equal("image/jpeg", VisionImageTextExtractor.MediaType("photo.jpg"));
    }

    static byte[] PngWithText(string keyword, string value)
    {
        byte[] data = Encoding.Latin1.GetBytes(keyword + "\0" + value);
        using MemoryStream stream = new();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "tEXt", data);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = [(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length];
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write([0, 0, 0, 0]);
    }

    static byte[] JpegWithComment(string comment)
    {
        byte[] data = Encoding.Latin1.GetBytes(comment);
        int length = data.Length + 2;
        return
        [
            0xFF, 0xD8,
            0xFF, 0xFE, (byte)(length >> 8), (byte)length,
            .. data,
            0xFF, 0xD9
        ];
    }

    sealed class FixedExtractor(string text) : IImageTextExtractor
    {
        public Task<string> ExtractAsync(Stream image, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(text);
    }

    sealed class ScriptedHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}

public class AzureHybridOptionsTests
{
    [Fact]
    public void CreateHybridOptions_SetsVectorFieldAndLimit()
    {
        SearchOptions options = AzureSearchSchema.CreateHybridOptions(new float[] { 1f, 0f }, 5);

        Assert.Equal(5, options.Size);
        VectorizedQuery query = Assert.IsType<VectorizedQuery>(Assert.Single(options.VectorSearch.Queries));
        Assert.Equal(5, query.KNearestNeighborsCount);
        Assert.Contains(nameof(TextChunk.Embedding), query.Fields);
    }
}

public class StreamingQueryTests
{
    [Fact]
    public async Task LocalStream_JoinsToPromptText()
    {
        LocalLlmService llm = new();
        AzureOpenAIChatRequest request = new()
        {
            Messages =
            [
                new SystemChatMessage("infer the user's real intent\n\nUser's query: How do I contribute?")
            ]
        };

        string prompted = (await llm.PromptAsync<AzureOpenAIChatResponse>(request, CancellationToken.None)).Text;
        StringBuilder streamed = new();
        await foreach (string token in llm.StreamAsync(request, CancellationToken.None))
            streamed.Append(token);

        Assert.Equal(prompted, streamed.ToString());
    }

    [Fact]
    public async Task AskStreamAsync_AppendsSourcesAfterTokens()
    {
        Mock<IRag> rag = new();
        rag.Setup(item => item.BuildRequestAsync<AzureOpenAIChatRequest>(
                It.IsAny<string>(),
                It.IsAny<Queue<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureOpenAIChatRequest
            {
                TextChunks = [new TextChunk { DocumentReference = "fs@guide.md" }]
            });

        AzureOpenAIQuery query = new(new TokenLlm(), rag.Object);
        StringBuilder streamed = new();
        await foreach (string token in query.AskStreamAsync("question", new Queue<ChatMessage>(), CancellationToken.None))
            streamed.Append(token);

        Assert.Equal("Hello world\n\nSources:\n- fs@guide.md", streamed.ToString().Replace("\r\n", "\n"));
    }

    sealed class TokenLlm : ILlm
    {
        public Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
            where TResponse : IChatResponse =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return "Hello ";
            yield return "world";
            await Task.CompletedTask;
        }
    }
}
