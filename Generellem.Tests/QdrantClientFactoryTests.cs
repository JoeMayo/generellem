using Generellem.Services.Qdrant;

using QdrantClient = global::Qdrant.Client.QdrantClient;

namespace Generellem.Services.Tests;

public class QdrantClientFactoryTests
{
    [Theory]
    [InlineData("http://localhost", QdrantClientFactory.DefaultGrpcPort)]
    [InlineData("https://example.cloud.qdrant.io", QdrantClientFactory.DefaultGrpcPort)]
    [InlineData("http://127.0.0.1:6334", 6334)]
    [InlineData("http://127.0.0.1:49152", 49152)]
    public void ResolveGrpcPort_UsesExplicitPortOrGrpcDefault(string endpoint, int expectedPort)
    {
        int port = QdrantClientFactory.ResolveGrpcPort(new Uri(endpoint));

        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsBlankEndpoint(string? endpoint)
    {
        Assert.ThrowsAny<ArgumentException>(() => QdrantClientFactory.Create(endpoint, "key"));
    }

    [Fact]
    public void Create_RejectsBlankApiKey()
    {
        Assert.ThrowsAny<ArgumentException>(() => QdrantClientFactory.Create("http://localhost:6334", " "));
    }

    [Fact]
    public void Create_BuildsAClientForAnExplicitPort()
    {
        using QdrantClient client = QdrantClientFactory.Create("http://127.0.0.1:6334", "test-key");

        Assert.NotNull(client);
    }
}
